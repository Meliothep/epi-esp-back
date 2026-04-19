using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using DnDiscordAPI.Messages.Hubs;
using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Inventory.DTOs;
using Multiplayer.Hubs;

namespace DnDiscordAPI.Messages.Services
{
    public class SignalRService
    {
        private readonly IHubContext<MessageHub> _hubContext;
        private readonly IHubContext<GameHub> _gameHubContext;
        private readonly ILogger<SignalRService> _logger;

        public SignalRService(
            IHubContext<MessageHub> hubContext,
            IHubContext<GameHub> gameHubContext,
            ILogger<SignalRService> logger)
        {
            _hubContext = hubContext;
            _gameHubContext = gameHubContext;
            _logger = logger;
        }

        /// <summary>
        /// Session-scoped inventory broadcast. Sends to the session group when known so
        /// only players in that group (plus the DM) are notified; skips broadcast when no
        /// session is active for the character (data is already persisted).
        /// Broadcast failures are swallowed — a SignalR hiccup must not fail the DB write
        /// that already succeeded.
        /// </summary>
        public async Task SendInventoryChangedAsync(string? sessionId, InventoryChangedEvent evt)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogDebug(
                    "Skipping InventoryChanged broadcast for character {CharacterId}: no active session",
                    evt.CharacterId);
                return;
            }

            try
            {
                _logger.LogDebug(
                    "Broadcasting InventoryChanged ({Action}) to session {SessionId} for character {CharacterId}",
                    evt.Action, sessionId, evt.CharacterId);
                await _gameHubContext.Clients.Group(sessionId).SendAsync("InventoryChanged", evt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to broadcast InventoryChanged for character {CharacterId}",
                    evt.CharacterId);
            }
        }

        /// <summary>
        /// Session-scoped "item used" notification — drives toast / sound effect while the
        /// actual inventory state change is already covered by InventoryChanged.
        /// </summary>
        public async Task SendInventoryItemUsedAsync(string? sessionId, InventoryItemUsedEvent evt)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            try
            {
                await _gameHubContext.Clients.Group(sessionId).SendAsync("InventoryItemUsed", evt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to broadcast InventoryItemUsed for character {CharacterId}",
                    evt.CharacterId);
            }
        }

        /// <summary>
        /// User-scoped wallet broadcast. <paramref name="ownerDiscordUserId"/> is the owner's
        /// Discord snowflake; routed through <see cref="DiscordUserIdProvider"/> so only that
        /// player's connections receive the event.
        /// </summary>
        public async Task SendWalletChangedAsync(string ownerDiscordUserId, Guid characterId, WalletDto wallet)
        {
            if (string.IsNullOrEmpty(ownerDiscordUserId))
            {
                _logger.LogWarning("Cannot broadcast WalletChanged for character {CharacterId}: owner id missing", characterId);
                return;
            }

            try
            {
                _logger.LogDebug("Broadcasting WalletChanged to user {UserId} for character {CharacterId}",
                    ownerDiscordUserId, characterId);
                await _gameHubContext.Clients.User(ownerDiscordUserId).SendAsync("WalletChanged",
                    new { characterId, wallet });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to broadcast WalletChanged for character {CharacterId}",
                    characterId);
            }
        }

        public async Task SendMessageToFront(MessageDto message)
        {
            if (message == null)
            {
                _logger.LogWarning("SendMessageToFront called with null message");
                return;
            }

            try
            {
                _logger.LogDebug("Broadcasting message author={Author} channel={Channel}",
                    message.Author, message.Channel);
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast chat message");
            }
        }
    }
}
