using System.Collections.Generic;
using System.Linq;
using Multiplayer.Models;

namespace Multiplayer.Models;

/// <summary>
/// Snapshot minimal de l'état de jeu, utilisable pour une resynchronisation
/// (reconnect / late join). Le contenu peut évoluer, mais doit rester sérialisable.
/// </summary>
/// <remarks>
/// TODO (type design): all properties are mutable — a "snapshot" that can be modified
/// after construction is a snapshot in name only. Convert to <c>record</c> with
/// <c>init</c>-only setters and <c>IReadOnlyList&lt;UnitRuntimeState&gt;</c> for Units.
/// Coordinate with the front before changing serialisation shape.
/// <para/>
/// TODO: <see cref="Units"/> and <see cref="Combat"/>.<c>Units</c> are two sources of
/// truth for the same data. Drop the top-level <see cref="Units"/> or make it a computed
/// alias for <c>Combat.Units.Values</c>.
/// </remarks>
public class GameStateSnapshot
{
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp at which the snapshot was produced. Clients can compare
    /// against a locally-cached snapshot to detect whether a received push is
    /// newer than what they already have (e.g. duplicate FullStateSync on rejoin).
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

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
            CapturedAt = DateTime.UtcNow,
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

