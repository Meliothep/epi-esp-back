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
        // Deep copy — caller must hold session.Combat.Lock before calling.
        var unitsCopy = session.Combat.Units.Values
            .Select(u => new UnitRuntimeState
            {
                UnitId = u.UnitId,
                OwnerUserId = u.OwnerUserId,
                Team = u.Team,
                Name = u.Name,
                CharacterClass = u.CharacterClass,
                PositionX = u.PositionX,
                PositionY = u.PositionY,
                CurrentHp = u.CurrentHp,
                MaxHp = u.MaxHp,
                CurrentAp = u.CurrentAp,
                MaxAp = u.MaxAp,
                Initiative = u.Initiative,
            })
            .ToList();

        return new GameStateSnapshot
        {
            SessionId = session.SessionId,
            Units = unitsCopy,
            Combat = new CombatState
            {
                Phase = session.Combat.Phase,
                Round = session.Combat.Round,
                CurrentUnitIndex = session.Combat.CurrentUnitIndex,
                TurnOrder = session.Combat.TurnOrder.ToList(),
                Outcome = session.Combat.Outcome,
                Units = unitsCopy.ToDictionary(u => u.UnitId),
            },
        };
    }
}

