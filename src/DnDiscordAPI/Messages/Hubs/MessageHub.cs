using Microsoft.AspNetCore.SignalR;

namespace DnDiscordAPI.Messages.Hubs
{
    public class MessageHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[MessageHub] Client connecté: {Context.ConnectionId}");
            Console.WriteLine($"[MessageHub] Nombre total de clients connectés: {Context.GetHttpContext()?.Connection?.Id}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[MessageHub] Client déconnecté: {Context.ConnectionId}");
            if (exception != null)
            {
                Console.WriteLine($"[MessageHub] Exception lors de la déconnexion: {exception.Message}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Envoyer un message à tous les clients connectés
        /// </summary>
        public async Task SendMessageToAll(MessageDto message)
        {
            Console.WriteLine($"[MessageHub] SendMessageToAll appelé pour: {message?.Content}");
            await Clients.All.SendAsync("ReceiveMessage", message);
            Console.WriteLine($"[MessageHub] Message envoyé à tous les clients");
        }
    }

    public class MessageDto
    {
        public string Content { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string AuthorId { get; set; } = string.Empty;
        public string? AuthorAvatar { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string Guild { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public string MessageId { get; set; } = string.Empty;
    }
}
