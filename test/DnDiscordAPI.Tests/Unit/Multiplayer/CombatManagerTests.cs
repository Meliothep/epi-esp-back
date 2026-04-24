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

    [Fact]
    public async Task EndTurn_skips_dead_units_and_lands_on_next_alive()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 30),
            Unit("e1", UnitTeam.Enemy, initiative: 20),
            Unit("e2", UnitTeam.Enemy, initiative: 10),
        });
        // Kill the next-in-line enemy; cursor should jump over it.
        session.Combat.Units["e1"].CurrentHp = 0;

        var result = await mgr.EndTurnAsync(session, "p1");

        Assert.NotNull(result);
        Assert.Equal("e2", result!.CurrentUnitId);
    }

    [Fact]
    public async Task StartCombat_twice_resets_prior_roster_cleanly()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 10),
        });
        // Second call with a completely different roster (the Play Again case).
        var result = await mgr.StartCombatAsync(session, new[]
        {
            Unit("p2", UnitTeam.Player, initiative: 15),
            Unit("e2", UnitTeam.Enemy, initiative: 5),
        });

        Assert.DoesNotContain("p1", session.Combat.Units.Keys);
        Assert.DoesNotContain("e1", session.Combat.Units.Keys);
        Assert.Contains("p2", session.Combat.Units.Keys);
        Assert.Contains("e2", session.Combat.Units.Keys);
        Assert.Equal(1, result.Round);
        Assert.Equal(2, result.TurnOrder.Count);
    }

    [Fact]
    public async Task ApplyAttack_rejected_during_FreeRoam_phase()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        // FreeRoam phase by default; seed units without StartCombat.
        session.Combat.Units["p1"] = Unit("p1", UnitTeam.Player, hp: 10, ap: 4);
        session.Combat.Units["e1"] = Unit("e1", UnitTeam.Enemy, hp: 10, ap: 4);

        var result = await mgr.ApplyAttackAsync(session, "p1", "e1", damage: 5, apCost: 1);

        Assert.Null(result);
        Assert.Equal(10, session.Combat.Units["e1"].CurrentHp);
    }

    [Fact]
    public async Task SpawnUnit_during_FreeRoam_does_not_touch_turn_order()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };

        await mgr.SpawnUnitAsync(session, Unit("e1", UnitTeam.Enemy));

        Assert.Contains("e1", session.Combat.Units.Keys);
        Assert.Empty(session.Combat.TurnOrder);
        Assert.Equal(CombatPhase.FreeRoam, session.Combat.Phase);
    }

    [Fact]
    public async Task Determinism_same_seed_same_roster_produces_same_turn_order()
    {
        var seed = 99;
        var roster = new[]
        {
            Unit("a", UnitTeam.Player, initiative: 10),
            Unit("b", UnitTeam.Player, initiative: 15),
            Unit("c", UnitTeam.Enemy, initiative: 12),
        };

        var m1 = MakeManager(seed);
        var m2 = MakeManager(seed);
        var s1 = new GameSession { SessionId = "s1" };
        var s2 = new GameSession { SessionId = "s2" };

        var r1 = await m1.StartCombatAsync(s1, roster.Select(u => CloneUnit(u)));
        var r2 = await m2.StartCombatAsync(s2, roster.Select(u => CloneUnit(u)));

        Assert.Equal(r1.TurnOrder, r2.TurnOrder);
        Assert.Equal(r1.CurrentUnitId, r2.CurrentUnitId);
    }

    [Fact]
    public async Task DetectOutcome_returns_victory_when_last_enemy_hp_zero_mid_combat()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, hp: 10, initiative: 1)
        });
        // Mimic DmAdjustHp: clamp HP to 0 outside the FSM.
        session.Combat.Units["e1"].CurrentHp = 0;

        var result = await mgr.DetectOutcomeAsync(session);

        Assert.NotNull(result);
        Assert.Equal(CombatPhase.Resolved, result!.Phase);
        Assert.Equal(CombatResult.Victory, result.Outcome);
        Assert.Equal(CombatPhase.Resolved, session.Combat.Phase);
        Assert.Equal(CombatResult.Victory, session.Combat.Outcome);
    }

    [Fact]
    public async Task DetectOutcome_returns_defeat_when_last_player_hp_zero_mid_combat()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 10, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 1)
        });
        session.Combat.Units["p1"].CurrentHp = 0;

        var result = await mgr.DetectOutcomeAsync(session);

        Assert.NotNull(result);
        Assert.Equal(CombatResult.Defeat, result!.Outcome);
        Assert.Equal(CombatPhase.Resolved, session.Combat.Phase);
    }

    [Fact]
    public async Task DetectOutcome_returns_null_when_both_sides_still_have_alive_units()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 1)
        });

        var result = await mgr.DetectOutcomeAsync(session);

        Assert.Null(result);
        Assert.NotEqual(CombatPhase.Resolved, session.Combat.Phase);
    }

    [Fact]
    public async Task DetectOutcome_noop_when_combat_is_free_roam_or_already_resolved()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        session.Combat.Units["e1"] = Unit("e1", UnitTeam.Enemy, hp: 0);
        // FreeRoam phase — even with a wiped roster, DetectOutcome must not flip state.

        var free = await mgr.DetectOutcomeAsync(session);
        Assert.Null(free);
        Assert.Equal(CombatPhase.FreeRoam, session.Combat.Phase);

        // Now force Resolved and assert the second call is also a no-op.
        session.Combat.Phase = CombatPhase.Resolved;
        session.Combat.Outcome = CombatResult.Victory;
        var resolved = await mgr.DetectOutcomeAsync(session);
        Assert.Null(resolved);
    }

    private static UnitRuntimeState CloneUnit(UnitRuntimeState u) => new()
    {
        UnitId = u.UnitId,
        Team = u.Team,
        CurrentHp = u.CurrentHp,
        MaxHp = u.MaxHp,
        CurrentAp = u.CurrentAp,
        MaxAp = u.MaxAp,
        Initiative = u.Initiative,
    };
}
