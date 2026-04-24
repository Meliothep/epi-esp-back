using System.Linq;
using Multiplayer.Models;
using Multiplayer.Services;
using Xunit;
using static Multiplayer.Define;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

public class TurnManagerTests
{
    private static UnitRuntimeState Unit(string id, int initiative, UnitTeam team = UnitTeam.Player)
        => new() { UnitId = id, Initiative = initiative, Team = team, CurrentHp = 10, MaxHp = 10 };

    [Fact]
    public void CalculateInitiative_deterministic_for_same_seed()
    {
        var units = new[]
        {
            Unit("a", 5),
            Unit("b", 3),
            Unit("c", 7),
            Unit("d", 1)
        };

        var tm1 = new TurnManager(seed: 42);
        var tm2 = new TurnManager(seed: 42);

        Assert.Equal(tm1.CalculateInitiative(units), tm2.CalculateInitiative(units));
    }

    [Fact]
    public void CalculateInitiative_excludes_dead_units()
    {
        var units = new[]
        {
            Unit("alive", 5),
            new UnitRuntimeState { UnitId = "dead", Initiative = 99, CurrentHp = 0, MaxHp = 10 }
        };

        var order = new TurnManager(seed: 1).CalculateInitiative(units);

        Assert.Single(order);
        Assert.Equal("alive", order[0]);
    }

    [Fact]
    public void CalculateInitiative_empty_roster_returns_empty_list()
    {
        var order = new TurnManager(seed: 1).CalculateInitiative(System.Array.Empty<UnitRuntimeState>());
        Assert.Empty(order);
    }

    [Fact]
    public void CalculateInitiative_contains_every_alive_unit_exactly_once()
    {
        var units = Enumerable.Range(0, 10).Select(i => Unit($"u{i}", i)).ToArray();
        var order = new TurnManager(seed: 7).CalculateInitiative(units);

        Assert.Equal(10, order.Count);
        Assert.Equal(10, order.Distinct().Count());
    }
}
