using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multiplayer.Models;
using Multiplayer.Models.Messages;
using static Multiplayer.Define;

namespace Multiplayer.Services
{
    /// <summary>
    /// Owns the combat-state FSM for a given <see cref="GameSession"/>. Every mutation goes
    /// through this service; the hub layer does not touch <see cref="CombatState"/> fields directly.
    /// </summary>
    public class CombatManager
    {
        private readonly TurnManager _turnManager;
        private readonly ILogger<CombatManager> _logger;

        public CombatManager(TurnManager turnManager, ILogger<CombatManager> logger)
        {
            _turnManager = turnManager;
            _logger = logger;
        }

        /// <summary>
        /// Seeds the session's combat state with the provided unit roster and rolls initiative.
        /// Phase advances to PlayerTurn or EnemyTurn depending on who sits first in the order.
        /// </summary>
        public async Task<CombatStartResult> StartCombatAsync(GameSession session, IEnumerable<UnitRuntimeState> units)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                combat.Units.Clear();
                foreach (var u in units)
                {
                    combat.Units[u.UnitId] = u;
                }

                combat.TurnOrder = _turnManager.CalculateInitiative(combat.Units.Values);
                combat.CurrentUnitIndex = 0;
                combat.Round = 1;
                combat.Outcome = null;
                combat.Phase = PhaseForUnit(combat, combat.CurrentUnitId);

                _logger.LogInformation("Combat started for session {SessionId} with {Count} units, first unit {FirstUnit}",
                    session.SessionId, combat.TurnOrder.Count, combat.CurrentUnitId);

                return new CombatStartResult(
                    Phase: combat.Phase,
                    Round: combat.Round,
                    TurnOrder: combat.TurnOrder.ToList(),
                    CurrentUnitId: combat.CurrentUnitId,
                    Units: combat.Units.Values.ToList());
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Advances the turn cursor to the next alive unit. Wraps the round and restores AP
        /// when the cursor rolls over. Returns null if combat has resolved.
        /// </summary>
        public async Task<TurnAdvanceResult?> EndTurnAsync(GameSession session, string callerUnitId)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                if (combat.Phase == CombatPhase.FreeRoam || combat.Phase == CombatPhase.Resolved)
                    return null;

                if (combat.CurrentUnitId != callerUnitId)
                {
                    _logger.LogWarning("EndTurn rejected — caller unit {CallerUnit} is not current {CurrentUnit}",
                        callerUnitId, combat.CurrentUnitId);
                    return null;
                }

                var outcome = CheckOutcome(combat);
                if (outcome.HasValue)
                {
                    combat.Phase = CombatPhase.Resolved;
                    combat.Outcome = outcome;
                    return new TurnAdvanceResult(combat.Phase, combat.Round, combat.CurrentUnitId, outcome);
                }

                var next = FindNextAliveIndex(combat);
                if (next == -1)
                {
                    // Re-check outcome before defaulting to Defeat: if both sides
                    // died simultaneously (mutual kill on the last unit), CheckOutcome
                    // returns null (no alive player, no alive enemy) and we'd wrongly
                    // declare Defeat. Use CheckOutcome as the authoritative resolver;
                    // only fall back to Defeat when it truly can't determine a winner.
                    var finalOutcome = CheckOutcome(combat) ?? CombatResult.Defeat;
                    combat.Phase = CombatPhase.Resolved;
                    combat.Outcome = finalOutcome;
                    return new TurnAdvanceResult(combat.Phase, combat.Round, null, combat.Outcome);
                }

                // next <= CurrentUnitIndex means the cursor wrapped around the list,
                // i.e. a new round started. Edge case: single alive unit has index 0
                // and next == 0 every time — this correctly increments the round on
                // each "turn" for a solo unit, which is the intended behaviour.
                if (next <= combat.CurrentUnitIndex)
                {
                    combat.Round++;
                    foreach (var u in combat.Units.Values) u.CurrentAp = u.MaxAp;
                }

                combat.CurrentUnitIndex = next;
                combat.Phase = PhaseForUnit(combat, combat.CurrentUnitId);
                return new TurnAdvanceResult(combat.Phase, combat.Round, combat.CurrentUnitId, null);
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Applies a move to a unit. Validates phase + current unit + AP. Returns the remaining AP
        /// or null if the move is rejected.
        /// </summary>
        public async Task<CombatMoveOutcome?> ApplyMoveAsync(GameSession session, string unitId, int targetX, int targetY, int apCost)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                if (!combat.Units.TryGetValue(unitId, out var unit)) return null;

                if (combat.Phase != CombatPhase.FreeRoam)
                {
                    if (combat.CurrentUnitId != unitId) return null;
                    if (unit.CurrentAp < apCost) return null;
                    unit.CurrentAp -= apCost;
                }

                unit.PositionX = targetX;
                unit.PositionY = targetY;
                return new CombatMoveOutcome(unitId, targetX, targetY, unit.CurrentAp);
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Applies damage from an attacker to a target. Returns the resulting HP and alive flag,
        /// or null if the attack is rejected.
        /// </summary>
        public async Task<CombatAttackOutcome?> ApplyAttackAsync(GameSession session, string attackerId, string targetId, int damage, int apCost)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                if (combat.Phase == CombatPhase.Resolved || combat.Phase == CombatPhase.FreeRoam) return null;
                if (combat.CurrentUnitId != attackerId) return null;

                if (!combat.Units.TryGetValue(attackerId, out var attacker)) return null;
                if (!combat.Units.TryGetValue(targetId, out var target)) return null;
                if (!target.IsAlive) return null;
                if (attacker.CurrentAp < apCost) return null;

                attacker.CurrentAp -= apCost;
                target.CurrentHp = Math.Max(0, target.CurrentHp - damage);

                return new CombatAttackOutcome(attackerId, targetId, damage, target.CurrentHp, target.IsAlive, attacker.CurrentAp);
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Inserts a unit during combat. Appended to the end of the turn order so it acts next round.
        /// </summary>
        public async Task SpawnUnitAsync(GameSession session, UnitRuntimeState unit)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                combat.Units[unit.UnitId] = unit;
                if (combat.Phase != CombatPhase.FreeRoam && !combat.TurnOrder.Contains(unit.UnitId))
                {
                    combat.TurnOrder.Add(unit.UnitId);
                }
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Re-evaluates the alive rosters and transitions to Resolved if one side has been wiped
        /// out. Returns the resolution payload when a transition fired, or null otherwise. Used by
        /// out-of-band HP mutations (<c>DmAdjustHp</c>) that can kill the last enemy / player
        /// without going through <c>EndTurnAsync</c>'s normal cursor-advance path.
        /// </summary>
        public async Task<TurnAdvanceResult?> DetectOutcomeAsync(GameSession session)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                if (combat.Phase == CombatPhase.FreeRoam || combat.Phase == CombatPhase.Resolved)
                    return null;

                var outcome = CheckOutcome(combat);
                if (!outcome.HasValue) return null;

                combat.Phase = CombatPhase.Resolved;
                combat.Outcome = outcome;
                return new TurnAdvanceResult(combat.Phase, combat.Round, combat.CurrentUnitId, outcome);
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Applies an ability use: deducts AP from the attacker and applies Damage/Heal effects to
        /// targets server-side. Returns the resolved payload (server-computed Effects + remaining AP),
        /// or null when the action is rejected (wrong turn, insufficient AP, resolved combat).
        /// </summary>
        public async Task<AbilityApplyResult?> ApplyAbilityAsync(
            GameSession session, string unitId, string abilityId,
            IReadOnlyList<AbilityEffect> effects, int apCost)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                if (combat.Phase == CombatPhase.Resolved) return null;
                if (combat.Phase != CombatPhase.FreeRoam && combat.CurrentUnitId != unitId) return null;

                if (!combat.Units.TryGetValue(unitId, out var attacker) || !attacker.IsAlive) return null;
                if (apCost < 0 || attacker.CurrentAp < apCost) return null;

                attacker.CurrentAp -= apCost;

                var resolvedEffects = new List<AbilityEffect>();
                foreach (var effect in effects)
                {
                    if (!combat.Units.TryGetValue(effect.TargetId, out var target)) continue;
                    var value = Math.Max(0, effect.Value);
                    int actualValue;
                    switch (effect.Type)
                    {
                        case "Damage":
                            var before = target.CurrentHp;
                            target.CurrentHp = Math.Max(0, target.CurrentHp - value);
                            actualValue = before - target.CurrentHp;
                            break;
                        case "Heal":
                            var beforeHeal = target.CurrentHp;
                            target.CurrentHp = Math.Min(target.MaxHp, target.CurrentHp + value);
                            actualValue = target.CurrentHp - beforeHeal;
                            break;
                        default:
                            actualValue = value;
                            break;
                    }
                    resolvedEffects.Add(new AbilityEffect { Type = effect.Type, TargetId = effect.TargetId, Value = actualValue });
                }

                return new AbilityApplyResult(unitId, abilityId, attacker.CurrentAp, resolvedEffects);
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        /// <summary>
        /// Forcibly ends combat (e.g. DM-triggered). Transitions back to FreeRoam and clears turn state.
        /// </summary>
        /// <remarks>
        /// Intentionally does NOT clear <see cref="CombatState.Units"/>: units remain in the roster
        /// so their HP / AP / position survive the transition and are still visible in free-roam mode.
        /// If a fresh unit set is needed (e.g. map switch), the caller must clear units separately
        /// or route through <see cref="StartCombatAsync"/> which rebuilds the roster from scratch.
        /// </remarks>
        public async Task EndCombatAsync(GameSession session)
        {
            await session.Combat.Lock.WaitAsync();
            try
            {
                session.LastActivityAt = DateTime.UtcNow;
                var combat = session.Combat;
                combat.Phase = CombatPhase.FreeRoam;
                combat.TurnOrder.Clear();
                combat.CurrentUnitIndex = 0;
                combat.Round = 0;
                combat.Outcome = null;
            }
            finally
            {
                session.Combat.Lock.Release();
            }
        }

        private static CombatPhase PhaseForUnit(CombatState combat, string? unitId)
        {
            if (unitId == null || !combat.Units.TryGetValue(unitId, out var unit))
                return CombatPhase.PlayerTurn;
            return unit.Team == UnitTeam.Enemy ? CombatPhase.EnemyTurn : CombatPhase.PlayerTurn;
        }

        private static CombatResult? CheckOutcome(CombatState combat)
        {
            var hasAliveEnemy = combat.Units.Values.Any(u => u.Team == UnitTeam.Enemy && u.IsAlive);
            var hasAlivePlayer = combat.Units.Values.Any(u => (u.Team == UnitTeam.Player || u.Team == UnitTeam.Ally) && u.IsAlive);
            if (!hasAliveEnemy && hasAlivePlayer) return CombatResult.Victory;
            if (!hasAlivePlayer && hasAliveEnemy) return CombatResult.Defeat;
            return null;
        }

        private static int FindNextAliveIndex(CombatState combat)
        {
            if (combat.TurnOrder.Count == 0) return -1;
            for (int offset = 1; offset <= combat.TurnOrder.Count; offset++)
            {
                var i = (combat.CurrentUnitIndex + offset) % combat.TurnOrder.Count;
                var id = combat.TurnOrder[i];
                if (combat.Units.TryGetValue(id, out var u) && u.IsAlive) return i;
            }
            return -1;
        }
    }

    public record CombatStartResult(
        CombatPhase Phase,
        int Round,
        List<string> TurnOrder,
        string? CurrentUnitId,
        List<UnitRuntimeState> Units);

    public record TurnAdvanceResult(CombatPhase Phase, int Round, string? CurrentUnitId, CombatResult? Outcome);

    public record CombatMoveOutcome(string UnitId, int X, int Y, int ApRemaining);

    public record CombatAttackOutcome(string AttackerId, string TargetId, int Damage, int TargetHp, bool TargetAlive, int AttackerApRemaining);

    // IReadOnlyList prevents consumers from mutating the resolved effects list
    // after the record is constructed (a mutable List<> could be modified mid-fanout).
    public record AbilityApplyResult(string UnitId, string AbilityId, int AttackerApRemaining, IReadOnlyList<AbilityEffect> Effects);
}
