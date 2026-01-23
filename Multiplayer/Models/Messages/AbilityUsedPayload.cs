using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Models.Messages
{
    /// <summary>
    /// Payload pour l'utilisation d'une capacité
    /// </summary>
    public class AbilityUsedPayload
    {
        /// <summary>
        /// ID de l'unité qui utilise la capacité
        /// </summary>
        public string UnitId { get; set; } = string.Empty;

        /// <summary>
        /// ID de la capacité utilisée
        /// </summary>
        public string AbilityId { get; set; } = string.Empty;

        /// <summary>
        /// IDs des cibles de la capacité
        /// </summary>
        public List<string> Targets { get; set; } = new();

        /// <summary>
        /// Résultat du lancer de dés (optionnel)
        /// </summary>
        public DiceResult? DiceResult { get; set; }

        /// <summary>
        /// Effets appliqués par la capacité
        /// </summary>
        public List<AbilityEffect> Effects { get; set; } = new();
    }

    /// <summary>
    /// Résultat d'un lancer de dés
    /// </summary>
    public class DiceResult
    {
        public int DiceType { get; set; } // Ex: 20 pour d20
        public int Roll { get; set; }
        public int Modifier { get; set; }
        public int Total => Roll + Modifier;
    }

    /// <summary>
    /// Effet appliqué par une capacité
    /// </summary>
    public class AbilityEffect
    {
        public string Type { get; set; } = string.Empty; // "Damage", "Heal", "Buff", "Debuff"
        public string TargetId { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
