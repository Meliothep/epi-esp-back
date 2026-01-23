using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Models.Messages
{
    /// <summary>
    /// Enveloppe générique pour tous les messages de jeu
    /// </summary>
    public class GameMessage<T> where T : class
    {
        /// <summary>
        /// Type de message (ex: "UnitMoved", "AbilityUsed")
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Numéro de séquence pour l'ordre des messages
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Timestamp UTC de création du message
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ID de la session concernée
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Charge utile du message (données spécifiques)
        /// </summary>
        public T Payload { get; set; } = default!;
    }
