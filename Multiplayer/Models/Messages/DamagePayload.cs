using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Models.Messages;

/// <summary>
/// Payload pour l'application de dégâts
/// </summary>
public class DamageAppliedPayload
{
    /// <summary>
    /// ID de la cible
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// Montant de dégâts
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Source des dégâts (ID de l'unité ou nom de la capacité)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Type de dégâts
    /// </summary>
    public string DamageType { get; set; } = string.Empty; // "Physical", "Magical", "Fire", etc.

    /// <summary>
    /// HP restants après les dégâts
    /// </summary>
    public int RemainingHp { get; set; }
}