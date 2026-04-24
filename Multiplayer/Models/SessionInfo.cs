using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models
{
    public class SessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        /// <summary>
        /// Code court partageable pour rejoindre la session (XXXX-XXXX).
        /// </summary>
        public string JoinCode { get; set; } = string.Empty;
        public Guid? CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public SessionState State { get; set; }
        public string? MapId { get; set; }
        public List<PlayerInfo> Players { get; set; } = new();
    }

    public class PlayerInfo
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public PlayerRole Role { get; set; }
        public ConnectionStatus Status { get; set; }
        public Guid? SelectedCharacterId { get; set; }
        public string? SelectedCharacterName { get; set; }

        /// <summary>Lobby quickstart preset key: "warrior" | "mage" | "archer" | null.</summary>
        public string? SelectedDefaultTemplate { get; set; }
    }
}
