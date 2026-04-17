using Microsoft.AspNetCore.SignalR;
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

        public SignalRService(IHubContext<MessageHub> hubContext, IHubContext<GameHub> gameHubContext)
        {
            _hubContext = hubContext;
            _gameHubContext = gameHubContext;
        }

        /// <summary>
        /// Diffuse un changement d'inventaire à tous les clients connectés via GameHub
        /// (le hub utilisé par le front pour le temps réel).
        /// </summary>
        public async Task SendInventoryChangedAsync(InventoryChangedEvent evt)
        {
            try
            {
                Console.WriteLine($"[SignalRService] InventoryChanged {evt.Action} character={evt.CharacterId} item={evt.Entry?.Item?.Name}");
                await _gameHubContext.Clients.All.SendAsync("InventoryChanged", evt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalRService] Erreur InventoryChanged: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Diffuse un changement de bourse à tous les clients connectés via GameHub.
        /// </summary>
        public async Task SendWalletChangedAsync(Guid characterId, WalletDto wallet)
        {
            try
            {
                Console.WriteLine($"[SignalRService] WalletChanged character={characterId}");
                await _gameHubContext.Clients.All.SendAsync("WalletChanged", new { characterId, wallet });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalRService] Erreur WalletChanged: {ex.Message}");
                throw;
            }
        }

        public async Task SendMessageToFront(MessageDto message)
        {
            try
            {
                if (message == null)
                {
                    Console.WriteLine("[SignalRService] Erreur: Message est null");
                    return;
                }

                Console.WriteLine($"[SignalRService] Envoi du message via SignalR:");
                Console.WriteLine($"  - Content: {message.Content?.Substring(0, Math.Min(50, message.Content?.Length ?? 0))}...");
                Console.WriteLine($"  - Author: {message.Author}");
                Console.WriteLine($"  - Channel: {message.Channel}");
                Console.WriteLine($"  - Guild: {message.Guild}");
                Console.WriteLine($"  - Timestamp: {message.Timestamp}");

                // Vérifier s'il y a des clients connectés (pour debug)
                var hubClients = _hubContext.Clients;
                Console.WriteLine($"[SignalRService] Tentative d'envoi à tous les clients connectés...");

                // Envoyer le message à tous les clients connectés via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", message);
                
                Console.WriteLine($"[SignalRService] Message envoyé avec succès via SignalR (méthode: ReceiveMessage)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignalRService] Erreur lors de l'envoi du message: {ex.Message}");
                Console.WriteLine($"[SignalRService] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}

