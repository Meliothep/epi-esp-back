using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models.Messages
{
    /// <summary>
    /// Payload pour la fin d'un tour
    /// </summary>
    public class TurnEndedPayload
    {
        /// <summary>
        /// ID de l'unité qui termine son tour
        /// </summary>
        public string UnitId { get; set; } = string.Empty;

        /// <summary>
        /// Server-authoritative fields filled in by the hub after advancing the
        /// cursor. Clients read these verbatim — no local nextTurn() call.
        /// </summary>
        public string? NextUnitId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CombatPhase Phase { get; set; } = CombatPhase.PlayerTurn;

        public int Round { get; set; } = 1;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CombatResult? Outcome { get; set; }
    }

    /// <summary>
    /// Payload pour le début d'un tour
    /// </summary>
    public class TurnStartedPayload
    {
        /// <summary>
        /// ID de l'unité dont c'est le tour
        /// </summary>
        public string UnitId { get; set; } = string.Empty;

        /// <summary>
        /// Numéro du round actuel
        /// </summary>
        public int RoundNumber { get; set; }

        /// <summary>
        /// Points d'action disponibles
        /// </summary>
        public int AvailableAp { get; set; }

        /// <summary>
        /// Actions disponibles ce tour
        /// </summary>
        public List<string> AvailableActions { get; set; } = new();
    }
}
