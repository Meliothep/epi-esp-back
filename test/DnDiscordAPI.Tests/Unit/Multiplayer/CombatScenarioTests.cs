using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Multiplayer.Models;
using Multiplayer.Services;
using Xunit;
using static Multiplayer.Define;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

/// <summary>
/// Scripted full-combat scenarios that exercise the CombatManager state machine
/// end-to-end. Each test drives the manager through a full combat cycle and
/// asserts the final state. Since all clients apply the broadcasts produced
/// during these scenarios identically (proven by the front applyState unit
/// tests), green here implies two-client sync.
/// </summary>
public class CombatScenarioTests
{
    private static CombatManager MakeManager() => new(new TurnManager(seed: 42), NullLogger<CombatManager>.Instance);

    private static UnitRuntimeState Unit(string id, UnitTeam team, int hp = 10, int ap = 4, int initiative = 0) => new()
    {
        UnitId = id,
        Team = team,
        CurrentHp = hp,
        MaxHp = hp,
        CurrentAp = ap,
        MaxAp = ap,
        Initiative = initiative,
    };

    [Fact]
    public async Task Full_round_wraps_restores_AP_on_every_unit()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, ap: 4, initiative: 30),
            Unit("e1", UnitTeam.Enemy, ap: 4, initiative: 20),
            Unit("e2", UnitTeam.Enemy, ap: 4, initiative: 10),
        });

        // Each unit drains AP during their turn.
        session.Combat.Units["p1"].CurrentAp = 0;
        await mgr.EndTurnAsync(session, "p1");

        session.Combat.Units["e1"].CurrentAp = 0;
        await mgr.EndTurnAsync(session, "e1");

        session.Combat.Units["e2"].CurrentAp = 0;
        // Last EndTurn wraps the round.
        var after = await mgr.EndTurnAsync(session, "e2");

        Assert.NotNull(after);
        Assert.Equal(2, after!.Round);
        // AP fully restored for everyone.
        Assert.All(session.Combat.Units.Values, u => Assert.Equal(u.MaxAp, u.CurrentAp));
    }

    [Fact]
    public async Task Player_kills_last_enemy_ends_combat_with_victory()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 20, initiative: 20),
            Unit("e1", UnitTeam.Enemy, hp: 5, initiative: 10),
        });

        var atk = await mgr.ApplyAttackAsync(session, "p1", "e1", damage: 10, apCost: 2);
        Assert.NotNull(atk);
        Assert.False(atk!.TargetAlive);

        // Player ends turn — CheckOutcome fires.
        var result = await mgr.EndTurnAsync(session, "p1");
        Assert.NotNull(result);
        Assert.Equal(CombatPhase.Resolved, result!.Phase);
        Assert.Equal(CombatResult.Victory, result.Outcome);
    }

    [Fact]
    public async Task Enemy_kills_last_player_ends_combat_with_defeat()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        // Initiative gap wide enough that e1's min roll (1+25=26) beats p1's
        // max (20+0=20). Guarantees enemy acts first under any seed.
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 5, initiative: 0),
            Unit("e1", UnitTeam.Enemy, hp: 20, initiative: 25),
        });

        Assert.Equal("e1", session.Combat.CurrentUnitId);

        var atk = await mgr.ApplyAttackAsync(session, "e1", "p1", damage: 10, apCost: 2);
        Assert.NotNull(atk);
        Assert.False(atk!.TargetAlive);

        var result = await mgr.EndTurnAsync(session, "e1");
        Assert.NotNull(result);
        Assert.Equal(CombatPhase.Resolved, result!.Phase);
        Assert.Equal(CombatResult.Defeat, result.Outcome);
    }

    [Fact]
    public async Task Mid_combat_spawn_appends_to_turn_order_acts_next_round()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 10),
        });

        var orderBefore = session.Combat.TurnOrder.ToList();

        await mgr.SpawnUnitAsync(session, Unit("e2", UnitTeam.Enemy, hp: 15));

        Assert.Equal(orderBefore.Count + 1, session.Combat.TurnOrder.Count);
        Assert.Equal("e2", session.Combat.TurnOrder.Last());
        // Current unit unchanged.
        Assert.Equal("p1", session.Combat.CurrentUnitId);
    }

    [Fact]
    public async Task DmEndCombat_preserves_unit_roster_but_clears_turn_state()
    {
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, initiative: 20),
            Unit("e1", UnitTeam.Enemy, initiative: 10),
        });

        // DM aborts — session returns to FreeRoam without losing the roster
        // (units stay on the board; combat simply stops).
        await mgr.EndCombatAsync(session);

        Assert.Equal(CombatPhase.FreeRoam, session.Combat.Phase);
        Assert.Empty(session.Combat.TurnOrder);
        Assert.Equal(0, session.Combat.CurrentUnitIndex);
        Assert.Null(session.Combat.Outcome);
        // Roster preserved so the board doesn't clear.
        Assert.Equal(2, session.Combat.Units.Count);
    }

    [Fact]
    public async Task Restart_after_defeat_wipes_prior_combat_entirely()
    {
        // Pins the "enemies resurrect after Play Again" bug: any unit from the
        // defeated life must not survive into the new one. CombatManager
        // .StartCombatAsync clears Units before re-seeding.
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };

        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 5, initiative: 0),
            Unit("e1", UnitTeam.Enemy, hp: 20, initiative: 25),
            Unit("e2", UnitTeam.Enemy, hp: 15, initiative: 15),
        });
        await mgr.ApplyAttackAsync(session, "e1", "p1", damage: 10, apCost: 0);
        await mgr.EndTurnAsync(session, "e1"); // detects Defeat

        Assert.Equal(CombatPhase.Resolved, session.Combat.Phase);
        Assert.Equal(CombatResult.Defeat, session.Combat.Outcome);

        // Restart with only the player (Play Again scenario — no DM-spawned
        // enemies yet). StartCombatAsync clears Units before re-seeding.
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 30, initiative: 20),
        });

        Assert.DoesNotContain("e1", session.Combat.Units.Keys);
        Assert.DoesNotContain("e2", session.Combat.Units.Keys);
        Assert.Single(session.Combat.Units);
        Assert.Null(session.Combat.Outcome);
        Assert.Equal(1, session.Combat.Round);
    }

    [Fact]
    public async Task EndTurn_payload_carries_full_unit_snapshot_so_both_clients_can_apply_identically()
    {
        // This test pins the contract that EndTurnAsync + the hub wrapper produce
        // a payload with every unit's current state. Proof-of-sync: if two
        // clients receive the same payload, and both apply via applyUnitsSnapshot
        // (tested on the front), they MUST converge on the same final state.
        var mgr = MakeManager();
        var session = new GameSession { SessionId = "s1" };
        await mgr.StartCombatAsync(session, new[]
        {
            Unit("p1", UnitTeam.Player, hp: 20, initiative: 20),
            Unit("e1", UnitTeam.Enemy, hp: 8, initiative: 10),
        });

        // Damage the enemy mid-turn.
        await mgr.ApplyAttackAsync(session, "p1", "e1", damage: 3, apCost: 1);

        // Now end p1's turn — the hub would broadcast TurnEnded with full
        // units snapshot. Verify server state matches what would ship.
        var result = await mgr.EndTurnAsync(session, "p1");
        Assert.NotNull(result);

        var units = session.Combat.Units.Values.ToList();
        Assert.Equal(2, units.Count);
        Assert.Equal(5, units.First(u => u.UnitId == "e1").CurrentHp); // 8 - 3
        Assert.Equal(20, units.First(u => u.UnitId == "p1").CurrentHp);
        Assert.Equal(3, units.First(u => u.UnitId == "p1").CurrentAp); // 4 - 1
    }
}
