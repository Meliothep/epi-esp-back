using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models.Messages;

/// <summary>
/// Payload pour les messages narratifs du DM
/// </summary>
public class DmNarrationPayload
{
    /// <summary>
    /// Texte de la narration
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Style de présentation
    /// </summary>
    public NarrationStyle Style { get; set; }

    /// <summary>
    /// Nom du PNJ (si style = NpcVoice)
    /// </summary>
    public string? NpcName { get; set; }

    /// <summary>
    /// ID du joueur cible (si whisper)
    /// </summary>
    public Guid? TargetPlayerId { get; set; }
}