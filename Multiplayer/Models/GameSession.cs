using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models
{
    public class GameSession
    {
        /// <summary>
        /// Dedicated lock for any mutation/iteration over <see cref="Players"/>.
        /// Do not lock on the List instance itself (it can be replaced).
        /// </summary>
        [JsonIgnore]
        public object PlayersLock { get; } = new();

        public string SessionId { get; set; } = string.Empty;
        /// <summary>
        /// Code court partageable pour rejoindre la session (XXXX-XXXX).
        /// Pour les "rooms", il peut être identique à SessionId.
        /// </summary>
        public string JoinCode { get; set; } = string.Empty;
        public Guid? CampaignId { get; set; }
        public Guid DmUserId { get; set; }
        public List<SessionPlayer> Players { get; set; } = new();
        /// <summary>
        /// Set to true right before the session is removed from in-memory indexes.
        /// Used to reject late JoinSession attempts racing with cleanup/removal.
        /// </summary>
        public bool IsTerminated { get; set; }
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

        /// <summary>
        /// In-flight roll requests keyed by request ID. Never serialized to wire format.
        /// </summary>
        [JsonIgnore]
        public ConcurrentDictionary<Guid, PendingRollRequest> PendingRolls { get; }
            = new();
    }
}
