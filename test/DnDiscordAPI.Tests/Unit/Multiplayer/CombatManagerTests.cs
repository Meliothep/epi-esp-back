using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Multiplayer.Models;
using Multiplayer.Services;
using Xunit;
using static Multiplayer.Define;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

public class CombatManagerTests
{
    private static CombatManager MakeManager(int seed = 42)
        => new(new TurnManager(seed), NullLogger<CombatManager>.Instance);

    private static UnitRuntimeState Unit(string id, UnitTeam team, int hp = 10, int ap = 4, int initiative = 0)
        => new()
        {
            UnitId = id,
            Team = team,
            CurrentHp = hp,
            MaxHp = hp,
            CurrentAp = ap,
            MaxAp = ap,
            Initiative = initiative
        };

    [Fact]
    public async Task StartCombat_populates_turn_order_and_sets_phase_to_current_unit_team()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };

        var result = await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 1)
        });

        Assert.Equal(2, result.TurnOrder.Count);
        Assert.Equal("p1", result.CurrentUnitId);
        Assert.Equal(CombatPhase.PlayerTurn, result.Phase);
        Assert.Equal(1, result.Round);
    }

    [Fact]
    public async Task EndTurn_rejects_when_caller_is_not_current_unit()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("p2", UnitTeam.Player, initiative: 1)
        });

        var result = await mgr.EndTurnAsync(session, "p2");

        Assert.Null(result);
        Assert.Equal("p1", session.Combat.CurrentUnitId);
    }

    [Fact]
    public async Task EndTurn_advances_round_and_restores_ap_when_cursor_wraps()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("a", UnitTeam.Player, ap: 4, initiative: 20),
            Unit("b", UnitTeam.Enemy, ap: 4, initiative: 10)
        });
        session.Combat.Units["a"].CurrentAp = 0;
        session.Combat.Units["b"].CurrentAp = 0;

        var first = session.Combat.CurrentUnitId!;
        await mgr.EndTurnAsync(session, first);
        var second = session.Combat.CurrentUnitId!;
        await mgr.EndTurnAsync(session, second);

        Assert.Equal(2, session.Combat.Round);
        Assert.All(session.Combat.Units.Values, u => Assert.Equal(u.MaxAp, u.CurrentAp));
    }

    [Fact]
    public async Task ApplyAttack_reduces_hp_and_marks_dead_when_zero()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 10, ap: 4, initiative: 20),
            Unit("e1", UnitTeam.Enemy, hp: 5, ap: 4, initiative: 1)
        });

        var outcome = await mgr.ApplyAttackAsync(session, "p1", "e1", damage: 10, apCost: 2);

        Assert.NotNull(outcome);
        Assert.Equal(0, outcome!.TargetHp);
        Assert.False(outcome.TargetAlive);
        Assert.Equal(2, session.Combat.Units["p1"].CurrentAp);
    }

    [Fact]
    public async Task EndTurn_after_last_enemy_dies_transitions_to_resolved_victory()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, hp: 1, initiative: 1)
        });
        session.Combat.Units["e1"].CurrentHp = 0;

        var result = await mgr.EndTurnAsync(session, "p1");

        Assert.NotNull(result);
        Assert.Equal(CombatPhase.Resolved, result!.Phase);
        Assert.Equal(CombatResult.Victory, result.Outcome);
    }

    [Fact]
    public async Task ApplyMove_rejects_when_ap_insufficient_in_combat()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, ap: 1, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 1)
        });

        var result = await mgr.ApplyMoveAsync(session, "p1", targetX: 5, targetY: 5, apCost: 3);

        Assert.Null(result);
        Assert.Equal(0, session.Combat.Units["p1"].PositionX);
    }

    [Fact]
    public async Task ApplyMove_free_roam_ignores_ap_and_phase_gates()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        session.Combat.Units["p1"] = Unit("p1", UnitTeam.Player, ap: 0);

        var result = await mgr.ApplyMoveAsync(session, "p1", targetX: 7, targetY: 3, apCost: 2);

        Assert.NotNull(result);
        Assert.Equal(7, session.Combat.Units["p1"].PositionX);
        Assert.Equal(0, session.Combat.Units["p1"].CurrentAp);
    }

    [Fact]
    public async Task SpawnUnit_during_combat_appends_to_turn_order()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 1)
        });

        await mgr.SpawnUnitAsync(session, Unit("e2", UnitTeam.Enemy));

        Assert.Contains("e2", session.Combat.TurnOrder);
        Assert.Equal("e2", session.Combat.TurnOrder.Last());
    }

    [Fact]
    public async Task EndCombat_clears_turn_state_and_returns_to_free_roam()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 1)
        });

        await mgr.EndCombatAsync(session);

        Assert.Equal(CombatPhase.FreeRoam, session.Combat.Phase);
        Assert.Empty(session.Combat.TurnOrder);
    }
}
