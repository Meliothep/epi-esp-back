using AutoMapper;
using DnDiscordAPI.Games.Database;
using DnDiscordAPI.Games.Inventory.DTOs;
using DnDiscordAPI.Games.Inventory.Models;
using DnDiscordAPI.Messages.Services;
using Microsoft.EntityFrameworkCore;
using Multiplayer.Services;

namespace DnDiscordAPI.Games.Inventory.Services
{
    public interface IInventoryService
    {
        Task<List<ItemDto>> GetCatalogAsync();
        Task<List<InventoryEntryDto>> GetCharacterInventoryAsync(Guid characterId);
        Task<InventoryEntryDto> GiveItemAsync(Guid characterId, GiveItemRequest request);
        Task RemoveEntryAsync(Guid characterId, Guid entryId, Guid? campaignId = null);
    }

    public class InventoryService : IInventoryService
    {
        private readonly GamesDbContext _context;
        private readonly IMapper _mapper;
        private readonly SignalRService _signalR;
        private readonly SessionManager _sessionManager;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(
            GamesDbContext context,
            IMapper mapper,
            SignalRService signalR,
            SessionManager sessionManager,
            ILogger<InventoryService> logger)
        {
            _context = context;
            _mapper = mapper;
            _signalR = signalR;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task<List<ItemDto>> GetCatalogAsync()
        {
            var items = await _context.Items.OrderBy(i => i.Name).ToListAsync();
            return _mapper.Map<List<ItemDto>>(items);
        }

        public async Task<List<InventoryEntryDto>> GetCharacterInventoryAsync(Guid characterId)
        {
            var entries = await _context.InventoryEntries
                .Include(e => e.Item)
                .Where(e => e.CharacterId == characterId)
                .OrderBy(e => e.Item!.Name)
                .ToListAsync();

            return _mapper.Map<List<InventoryEntryDto>>(entries);
        }

        public async Task<InventoryEntryDto> GiveItemAsync(Guid characterId, GiveItemRequest request)
        {
            var character = await _context.Characters.FindAsync(characterId)
                ?? throw new KeyNotFoundException($"Character {characterId} not found");

            var item = await _context.Items.FindAsync(request.ItemId)
                ?? throw new KeyNotFoundException($"Item {request.ItemId} not found");

            // POC : on empile sur une entrée existante si elle existe déjà pour ce perso/objet.
            var existing = await _context.InventoryEntries
                .Include(e => e.Item)
                .FirstOrDefaultAsync(e => e.CharacterId == characterId && e.ItemId == request.ItemId);

            InventoryEntry entry;
            InventoryChangeAction action;
            if (existing != null)
            {
                existing.Quantity += request.Quantity;
                entry = existing;
                action = InventoryChangeAction.Updated;
            }
            else
            {
                entry = new InventoryEntry
                {
                    Id = Guid.NewGuid(),
                    CharacterId = characterId,
                    ItemId = request.ItemId,
                    Quantity = request.Quantity,
                    Item = item,
                };
                _context.InventoryEntries.Add(entry);
                action = InventoryChangeAction.Added;
            }

            await _context.SaveChangesAsync();

            var dto = _mapper.Map<InventoryEntryDto>(entry);
            await _signalR.SendInventoryChangedAsync(ResolveSessionId(request.CampaignId), new InventoryChangedEvent
            {
                CharacterId = characterId,
                Action = action,
                Entry = dto,
            });

            _logger.LogInformation("Gave {Qty}x {Item} to character {Character}", request.Quantity, item.Name, characterId);
            return dto;
        }

        public async Task RemoveEntryAsync(Guid characterId, Guid entryId, Guid? campaignId = null)
        {
            var entry = await _context.InventoryEntries
                .Include(e => e.Item)
                .FirstOrDefaultAsync(e => e.Id == entryId && e.CharacterId == characterId)
                ?? throw new KeyNotFoundException($"Inventory entry {entryId} not found for character {characterId}");

            var sessionId = ResolveSessionId(campaignId);

            if (entry.Quantity > 1)
            {
                entry.Quantity -= 1;
                await _context.SaveChangesAsync();

                var updatedDto = _mapper.Map<InventoryEntryDto>(entry);
                await _signalR.SendInventoryChangedAsync(sessionId, new InventoryChangedEvent
                {
                    CharacterId = characterId,
                    Action = InventoryChangeAction.Updated,
                    Entry = updatedDto,
                });

                _logger.LogInformation("Decremented inventory entry {Entry} for character {Character}, qty now {Qty}", entryId, characterId, entry.Quantity);
            }
            else
            {
                var dto = _mapper.Map<InventoryEntryDto>(entry);
                _context.InventoryEntries.Remove(entry);
                await _context.SaveChangesAsync();

                await _signalR.SendInventoryChangedAsync(sessionId, new InventoryChangedEvent
                {
                    CharacterId = characterId,
                    Action = InventoryChangeAction.Removed,
                    Entry = dto,
                });

                _logger.LogInformation("Removed inventory entry {Entry} from character {Character}", entryId, characterId);
            }
        }

        /// <summary>
        /// Finds the SignalR group id for an active session tied to the campaign.
        /// Returns null if no campaign id is known or no live session exists — the caller
        /// will then skip the broadcast while the DB write still happens.
        /// </summary>
        private string? ResolveSessionId(Guid? campaignId)
        {
            if (!campaignId.HasValue) return null;

            var session = _sessionManager
                .GetSessionsByCampaign(campaignId.Value)
                .FirstOrDefault();

            return session?.SessionId;
        }
    }
}
