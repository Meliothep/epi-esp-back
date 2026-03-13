using Microsoft.AspNetCore.SignalR;
using DnDiscordAPI.Messages.Hubs;

namespace DnDiscordAPI.Messages.Services
{
    public class SignalRService
    {
        private readonly IHubContext<MessageHub> _hubContext;

        public SignalRService(IHubContext<MessageHub> hubContext)
        {
            _hubContext = hubContext;
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

