using System.Collections.Generic;
using System.Linq;
using Multiplayer.Services;
using Xunit;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

public class SpawnPlacementTests
{
    private static SpawnPlacementService Make() => new();

    // ---------------------------------------------------------------------------
    // BlocksMovement
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("wall")]
    [InlineData("block")]
    [InlineData("obstacle")]
    [InlineData("furniture")]
    [InlineData("decoration")]
    [InlineData("nature")]
    public void BlocksMovement_returns_true_for_blocking_types(string assetType)
    {
        Assert.True(SpawnPlacementService.BlocksMovement(assetType));
    }

    [Theory]
    [InlineData("floor")]
    [InlineData("water")]
    [InlineData("lava")]
    [InlineData("resource")]
    [InlineData("character")]
    [InlineData("enemy")]
    public void BlocksMovement_returns_false_for_walkable_types(string assetType)
    {
        Assert.False(SpawnPlacementService.BlocksMovement(assetType));
    }

    [Fact]
    public void BlocksMovement_returns_false_for_unknown_asset_type()
    {
        Assert.False(SpawnPlacementService.BlocksMovement("totally_unknown_thing"));
    }

    // ---------------------------------------------------------------------------
    // BuildWalkableGrid
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildWalkableGrid_marks_wall_asset_cell_as_non_walkable()
    {
        var json = """
            {
              "id": "m1", "name": "Test", "createdAt": 0, "updatedAt": 0,
              "cells": [
                { "x": 3, "z": 4, "stackedAssets": [{ "assetType": "wall", "assetId": "w1", "assetPath": "", "scale": 1, "rotationY": 0, "positionY": 0 }] }
              ]
            }
            """;
        var svc = Make();
        var (walkable, _, _, _) = svc.BuildWalkableGrid(json);
        Assert.False(walkable["3,4"]);
    }

    [Fact]
    public void BuildWalkableGrid_returns_10x10_all_walkable_on_null_input()
    {
        var svc = Make();
        var (walkable, spawnZones, w, h) = svc.BuildWalkableGrid(null);
        Assert.Empty(walkable);
        Assert.Empty(spawnZones);
        Assert.Equal(10, w);
        Assert.Equal(10, h);
    }

    [Fact]
    public void BuildWalkableGrid_respects_affectedCells_propagation()
    {
        var json = """
            {
              "id": "m1", "name": "Test", "createdAt": 0, "updatedAt": 0,
              "cells": [
                {
                  "x": 1, "z": 1,
                  "stackedAssets": [
                    {
                      "assetType": "wall", "assetId": "w1", "assetPath": "", "scale": 1, "rotationY": 0, "positionY": 0,
                      "affectedCells": [{ "x": 2, "z": 1 }, { "x": 3, "z": 1 }, { "x": 4, "z": 1 }]
                    }
                  ]
                }
              ]
            }
            """;
        var svc = Make();
        var (walkable, _, _, _) = svc.BuildWalkableGrid(json);
        Assert.False(walkable["2,1"]);
        Assert.False(walkable["3,1"]);
        Assert.False(walkable["4,1"]);
    }

    [Fact]
    public void BuildWalkableGrid_teleport_zone_overrides_stacked_wall()
    {
        var json = """
            {
              "id": "m1", "name": "Test", "createdAt": 0, "updatedAt": 0,
              "cells": [
                { "x": 5, "z": 5, "stackedAssets": [{ "assetType": "wall", "assetId": "w1", "assetPath": "", "scale": 1, "rotationY": 0, "positionY": 0 }] }
              ],
              "spawnZones": { "5,5": "teleport" }
            }
            """;
        var svc = Make();
        var (walkable, spawnZones, _, _) = svc.BuildWalkableGrid(json);
        Assert.True(walkable["5,5"]);
        Assert.Equal("teleport", spawnZones["5,5"]);
    }

    // ---------------------------------------------------------------------------
    // GetSpawnPositions — basic guards
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetSpawnPositions_returns_empty_when_count_zero_or_negative(int count)
    {
        var svc = Make();
        var walkable = AllWalkable(10, 10);
        var result = svc.GetSpawnPositions(walkable, new Dictionary<string, string>(), new HashSet<string>(), "ally", count, 10, 10, 42);
        Assert.Empty(result);
    }

    [Fact]
    public void GetSpawnPositions_returns_empty_when_walkable_empty()
    {
        var svc = Make();
        var result = svc.GetSpawnPositions(new Dictionary<string, bool>(), new Dictionary<string, string>(), new HashSet<string>(), "ally", 3, 10, 10, 42);
        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // GetSpawnPositions — curated zones
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSpawnPositions_respects_curated_spawn_zones_when_present()
    {
        var svc = Make();
        var walkable = AllWalkable(10, 10);
        var zones = new Dictionary<string, string> { ["4,1"] = "ally" };
        var result = svc.GetSpawnPositions(walkable, zones, new HashSet<string>(), "ally", 1, 10, 10, 42);
        Assert.Single(result);
        Assert.Equal(4, result[0].X);
        Assert.Equal(1, result[0].Y);
    }

    // ---------------------------------------------------------------------------
    // GetSpawnPositions — band fallback
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSpawnPositions_falls_back_to_band_when_no_zones()
    {
        var svc = Make();
        var walkable = AllWalkable(10, 10);
        var result = svc.GetSpawnPositions(walkable, new Dictionary<string, string>(), new HashSet<string>(), "ally", 3, 10, 10, 42);
        Assert.Equal(3, result.Count);
        // ally band: z in [0, floor(10*0.3)-1] = [0, 2]
        Assert.All(result, p => Assert.InRange(p.Y, 0, 2));
    }

    // ---------------------------------------------------------------------------
    // GetSpawnPositions — eligibility filters
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSpawnPositions_skips_non_walkable_and_occupied_cells()
    {
        var svc = Make();
        // Only one walkable ally cell at (2,0); rest of band blocked.
        var walkable = AllWalkable(10, 10);
        // Block entire band row z=0 and z=2, leave only z=1 row.
        for (var x = 0; x < 10; x++)
        {
            walkable[$"{x},0"] = false;
            walkable[$"{x},2"] = false;
        }
        // Occupy all z=1 except (2,1).
        var occupied = new HashSet<string>();
        for (var x = 0; x < 10; x++)
            if (x != 2)
                occupied.Add($"{x},1");

        var result = svc.GetSpawnPositions(walkable, new Dictionary<string, string>(), occupied, "ally", 1, 10, 10, 42);
        Assert.Single(result);
        Assert.Equal(2, result[0].X);
        Assert.Equal(1, result[0].Y);
    }

    // ---------------------------------------------------------------------------
    // GetSpawnPositions — determinism
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSpawnPositions_is_deterministic_for_same_seed()
    {
        var svc = Make();
        var walkable = AllWalkable(10, 10);
        var zones = new Dictionary<string, string>();
        var occupied = new HashSet<string>();

        var r1 = svc.GetSpawnPositions(walkable, zones, occupied, "ally", 5, 10, 10, 12345);
        var r2 = svc.GetSpawnPositions(walkable, zones, occupied, "ally", 5, 10, 10, 12345);
        Assert.Equal(r1.Select(p => $"{p.X},{p.Y}"), r2.Select(p => $"{p.X},{p.Y}"));

        var r3 = svc.GetSpawnPositions(walkable, zones, occupied, "ally", 5, 10, 10, 99999);
        // Different seed should produce a different order (extremely likely for any reasonable grid).
        var r1Keys = r1.Select(p => $"{p.X},{p.Y}").ToList();
        var r3Keys = r3.Select(p => $"{p.X},{p.Y}").ToList();
        Assert.NotEqual(r1Keys, r3Keys);
    }

    // ---------------------------------------------------------------------------
    // GetSpawnPositions — BFS fills remaining
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSpawnPositions_bfs_fills_remaining_when_band_mostly_blocked()
    {
        var svc = Make();
        // 10x10; ally band is z in [0,2]. Leave only 1 walkable cell in band.
        var walkable = AllWalkable(10, 10);
        for (var x = 0; x < 10; x++)
            for (var z = 0; z <= 2; z++)
                walkable[$"{x},{z}"] = false;
        // Allow just one band cell.
        walkable["5,1"] = true;

        // Request 3 positions; BFS must reach outside the band.
        var result = svc.GetSpawnPositions(walkable, new Dictionary<string, string>(), new HashSet<string>(), "ally", 3, 10, 10, 42);
        Assert.Equal(3, result.Count);
        // All returned positions must be distinct.
        var keys = result.Select(p => $"{p.X},{p.Y}").ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // ---------------------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------------------

    private static Dictionary<string, bool> AllWalkable(int width, int height)
    {
        var d = new Dictionary<string, bool>();
        for (var x = 0; x < width; x++)
            for (var z = 0; z < height; z++)
                d[$"{x},{z}"] = true;
        return d;
    }
}
