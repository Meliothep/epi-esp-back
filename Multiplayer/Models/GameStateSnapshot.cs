using System.Collections.Generic;
using System.Linq;
using Multiplayer.Models;

namespace Multiplayer.Models;

/// <summary>
/// Snapshot minimal de l'état de jeu, utilisable pour une resynchronisation
/// (reconnect / late join). Le contenu peut évoluer, mais doit rester sérialisable.
/// </summary>
public class GameStateSnapshot
{
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Liste des unités (positions / HP / AP) au moment du snapshot.
    /// </summary>
    public List<UnitRuntimeState> Units { get; set; } = new();

    /// <summary>
    /// Etat de combat server-authoritative (phase, ordre, curseur, unités).
    /// </summary>
    public CombatState Combat { get; set; } = new();

    public static GameStateSnapshot FromSession(GameSession session)
    {
        return new GameStateSnapshot
        {
            SessionId = session.SessionId,
            Units = session.Combat.Units.Values.ToList(),
            Combat = session.Combat
        };
    }
}

