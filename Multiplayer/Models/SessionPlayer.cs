using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models
{
    public class SessionPlayer
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? ConnectionId { get; set; }
        public PlayerRole Role { get; set; }
        public ConnectionStatus Status { get; set; } = ConnectionStatus.Connected;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DisconnectedAt { get; set; }
    }
}
