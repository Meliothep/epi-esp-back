using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models
{
    public class GameSession
    {
        public string SessionId { get; set; } = string.Empty;
        public Guid CampaignId { get; set; }
        public Guid DmUserId { get; set; }
        public List<SessionPlayer> Players { get; set; } = new();
        public SessionState State { get; set; } = SessionState.Lobby;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public int MaxPlayers { get; set; } = 6; // 5 joueurs + 1 DM
    }
}
