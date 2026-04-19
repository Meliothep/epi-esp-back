using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.Services;
using DnDiscordAPI.Games.Character.Services;
using DnDiscordAPI.Games.Inventory.DTOs;
using DnDiscordAPI.Games.Inventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Games.Controllers
{
    [ApiController]
    [Route("api/games/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly ICharacterService _characterService;
        private readonly ICampaignService _campaignService;
        private readonly IUserContextService _userContext;

        public InventoryController(
            IInventoryService inventoryService,
            ICharacterService characterService,
            ICampaignService campaignService,
            IUserContextService userContext)
        {
            _inventoryService = inventoryService;
            _characterService = characterService;
            _campaignService = campaignService;
            _userContext = userContext;
        }

        /// <summary>
        /// Catalogue des objets disponibles (POC, 12 objets seedés).
        /// </summary>
        [HttpGet("catalog")]
        public async Task<ActionResult<List<ItemDto>>> GetCatalog()
        {
            var items = await _inventoryService.GetCatalogAsync();
            return Ok(items);
        }

        /// <summary>
        /// Liste les entrées d'inventaire d'un personnage. Accessible au propriétaire
        /// du personnage. Le DM peut aussi lire via <c>?campaignId={id}</c>.
        /// </summary>
        [HttpGet("{characterId:guid}")]
        public async Task<ActionResult<List<InventoryEntryDto>>> GetCharacterInventory(
            Guid characterId,
            [FromQuery] Guid? campaignId,
            CancellationToken ct)
        {
            if (!await CanAccessCharacterAsync(characterId, campaignId, ct))
                return Forbid();

            var entries = await _inventoryService.GetCharacterInventoryAsync(characterId);
            return Ok(entries);
        }

        /// <summary>
        /// Ajoute un objet à l'inventaire d'un personnage (MJ → joueur).
        /// Seul le MJ de la campagne indiquée peut appeler cet endpoint.
        /// </summary>
        [HttpPost("{characterId:guid}")]
        public async Task<ActionResult<InventoryEntryDto>> GiveItem(
            Guid characterId,
            [FromBody] GiveItemRequest request,
            CancellationToken ct)
        {
            if (!await _campaignService.IsDungeonMasterAsync(request.CampaignId, _userContext.GetCurrentUserId(), ct))
                return Forbid();

            try
            {
                var entry = await _inventoryService.GiveItemAsync(characterId, request);
                return Ok(entry);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Supprime une entrée d'inventaire. Accessible au propriétaire (jet) ou au
        /// MJ de la campagne via <c>?campaignId={id}</c>.
        /// </summary>
        [HttpDelete("{characterId:guid}/entry/{entryId:guid}")]
        public async Task<IActionResult> RemoveEntry(
            Guid characterId,
            Guid entryId,
            [FromQuery] Guid? campaignId,
            CancellationToken ct)
        {
            if (!await CanAccessCharacterAsync(characterId, campaignId, ct))
                return Forbid();

            try
            {
                await _inventoryService.RemoveEntryAsync(characterId, entryId, campaignId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Consume one unit of an inventory entry (potion drunk, scroll read, …).
        /// Owner-only — DMs can't use a player's consumable on their behalf.
        /// </summary>
        [HttpPost("{characterId:guid}/entry/{entryId:guid}/use")]
        public async Task<IActionResult> UseEntry(
            Guid characterId,
            Guid entryId,
            [FromQuery] Guid? campaignId)
        {
            var ownerDiscordId = await _characterService.GetOwnerDiscordIdAsync(characterId);
            if (ownerDiscordId == null || ownerDiscordId != _userContext.GetCurrentDiscordUserId())
                return Forbid();

            try
            {
                await _inventoryService.UseEntryAsync(characterId, entryId, campaignId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// True if the caller owns the character, or is DM of the provided campaign.
        /// </summary>
        private async Task<bool> CanAccessCharacterAsync(Guid characterId, Guid? campaignId, CancellationToken ct)
        {
            var ownerDiscordId = await _characterService.GetOwnerDiscordIdAsync(characterId);
            if (ownerDiscordId != null && ownerDiscordId == _userContext.GetCurrentDiscordUserId())
                return true;

            if (campaignId.HasValue
                && await _campaignService.IsDungeonMasterAsync(campaignId.Value, _userContext.GetCurrentUserId(), ct))
                return true;

            return false;
        }
    }
}
