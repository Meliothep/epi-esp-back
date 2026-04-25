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
        public Guid? SelectedCharacterId { get; set; }

        /// <summary>Nom du personnage sélectionné (résolu à la sélection pour éviter un lookup à chaque broadcast).</summary>
        public string? SelectedCharacterName { get; set; }

        /// <summary>
        /// A preset template ("warrior" | "mage" | "archer") the player picked in
        /// the lobby as a no-persisted-character quickstart. Mutually exclusive
        /// with <see cref="SelectedCharacterId"/> — whichever is set most recently
        /// wins, the other is cleared. Null means "fall back to the default warrior
        /// assignment" (existing behaviour).
        /// </summary>
        public string? SelectedDefaultTemplate { get; set; }
    }
}
