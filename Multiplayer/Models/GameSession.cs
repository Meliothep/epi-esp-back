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
        /// <summary>
        /// Code court partageable pour rejoindre la session (XXXX-XXXX).
        /// Pour les "rooms", il peut être identique à SessionId.
        /// </summary>
        public string JoinCode { get; set; } = string.Empty;
        public Guid? CampaignId { get; set; }
        public Guid DmUserId { get; set; }
        public List<SessionPlayer> Players { get; set; } = new();
        public SessionState State { get; set; } = SessionState.Lobby;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
        public DateTime? DmDisconnectedAt { get; set; }
        public int MaxPlayers { get; set; } = 6; // 5 joueurs + 1 DM
        public string? MapId { get; set; }

        /// <summary>
        /// Server-authoritative combat state. Phase defaults to <see cref="CombatPhase.FreeRoam"/>;
        /// the <see cref="Services.CombatManager"/> is the only writer.
        /// </summary>
        public CombatState Combat { get; set; } = new();
    }
}
