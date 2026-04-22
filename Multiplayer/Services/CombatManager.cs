using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multiplayer.Models;
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
                    combat.Phase = CombatPhase.Resolved;
                    combat.Outcome = CombatResult.Defeat;
                    return new TurnAdvanceResult(combat.Phase, combat.Round, null, combat.Outcome);
                }

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
        /// Forcibly ends combat (e.g. DM-triggered). Transitions back to FreeRoam and clears turn state.
        /// </summary>
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
}
