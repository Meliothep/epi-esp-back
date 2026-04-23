using System.Text.Json;

namespace Multiplayer.Services;

public record GridPosition(int X, int Y);

public class SpawnPlacementService
{
    private const int GRID_SIZE = 10;

    /// <summary>
    /// Blocking asset types ported from CollisionUtils.ts::getCollisionProperties.
    /// </summary>
    public static bool BlocksMovement(string assetType) => assetType switch
    {
        "wall"        => true,
        "block"       => true,
        "obstacle"    => true,
        "furniture"   => true,
        "decoration"  => true,
        "nature"      => true,
        _             => false,
    };

    /// <summary>
    /// Parses <paramref name="mapDataJson"/> (the front's SavedMapData blob) and returns
    /// walkability + spawn zone dictionaries. Keys use "x,z" format.
    /// Returns empty dicts + 10x10 on null / invalid input.
    /// </summary>
    public (Dictionary<string, bool> Walkable, Dictionary<string, string> SpawnZones, int GridWidth, int GridHeight)
        BuildWalkableGrid(string? mapDataJson)
    {
        var walkable = new Dictionary<string, bool>();
        var spawnZones = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(mapDataJson))
            return (walkable, spawnZones, GRID_SIZE, GRID_SIZE);

        try
        {
            using var doc = JsonDocument.Parse(mapDataJson);
            var root = doc.RootElement;

            // Initialize all 100 cells walkable.
            for (var x = 0; x < GRID_SIZE; x++)
                for (var z = 0; z < GRID_SIZE; z++)
                    walkable[$"{x},{z}"] = true;

            // Collect teleport cells first so we can override at the end.
            var teleportCells = new HashSet<string>();
            if (root.TryGetProperty("spawnZones", out var zonesEl) && zonesEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in zonesEl.EnumerateObject())
                {
                    var zoneType = prop.Value.GetString() ?? string.Empty;
                    spawnZones[prop.Name] = zoneType;
                    if (zoneType == "teleport")
                        teleportCells.Add(prop.Name);
                }
            }

            // Process cells array.
            if (root.TryGetProperty("cells", out var cellsEl) && cellsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var cellEl in cellsEl.EnumerateArray())
                {
                    if (!cellEl.TryGetProperty("x", out var xEl) || !xEl.TryGetInt32(out var cx)) continue;
                    if (!cellEl.TryGetProperty("z", out var zEl) || !zEl.TryGetInt32(out var cz)) continue;
                    var cellKey = $"{cx},{cz}";

                    // Ground asset.
                    if (cellEl.TryGetProperty("ground", out var groundEl) && groundEl.ValueKind == JsonValueKind.Object)
                    {
                        var groundType = groundEl.TryGetProperty("assetType", out var gt) ? gt.GetString() ?? string.Empty : string.Empty;
                        if (BlocksMovement(groundType))
                        {
                            walkable[cellKey] = false;
                            PropagateAffectedCells(groundEl, walkable, nonWalkable: true);
                        }
                        else
                        {
                            PropagateAffectedCells(groundEl, walkable, nonWalkable: false);
                        }
                    }

                    // Stacked assets — first blocking one wins; non-blocking ones still propagate.
                    if (cellEl.TryGetProperty("stackedAssets", out var stackedEl) && stackedEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var assetEl in stackedEl.EnumerateArray())
                        {
                            var assetType = assetEl.TryGetProperty("assetType", out var at) ? at.GetString() ?? string.Empty : string.Empty;
                            if (BlocksMovement(assetType))
                            {
                                walkable[cellKey] = false;
                                PropagateAffectedCells(assetEl, walkable, nonWalkable: true);
                                break;
                            }
                            else
                            {
                                PropagateAffectedCells(assetEl, walkable, nonWalkable: false);
                            }
                        }
                    }
                }
            }

            // Teleport zones override non-walkable (step 4 of spec).
            foreach (var key in teleportCells)
                walkable[key] = true;
        }
        catch
        {
            // Invalid JSON — return empty.
            return (new Dictionary<string, bool>(), new Dictionary<string, string>(), GRID_SIZE, GRID_SIZE);
        }

        return (walkable, spawnZones, GRID_SIZE, GRID_SIZE);
    }

    /// <summary>
    /// Computes ally or enemy spawn positions using seeded Fisher-Yates shuffle + BFS fallback.
    /// Ported from Placement.ts::getSpawnPositions.
    /// </summary>
    public List<GridPosition> GetSpawnPositions(
        Dictionary<string, bool> walkable,
        IReadOnlyDictionary<string, string> spawnZones,
        ISet<string> occupied,
        string team,
        int count,
        int gridWidth,
        int gridHeight,
        int seed)
    {
        if (count <= 0) return new List<GridPosition>();
        if (gridWidth <= 0 || gridHeight <= 0) return new List<GridPosition>();
        if (walkable.Count == 0) return new List<GridPosition>();

        // Band range.
        var band = Math.Max(1, (int)Math.Floor(gridHeight * 0.3));
        int zMin, zMax;
        if (team == "ally")
        {
            zMin = 0;
            zMax = band - 1;
        }
        else
        {
            zMin = gridHeight - band;
            zMax = gridHeight - 1;
        }

        // Build candidates: curated zones when present, else band cells.
        var zoneCandidates = spawnZones
            .Where(kv => kv.Value == team)
            .Select(kv => ParseKey(kv.Key))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        List<GridPosition> candidates;
        if (zoneCandidates.Count > 0)
        {
            candidates = zoneCandidates
                .Where(p => IsEligible(p.X, p.Y, walkable, occupied))
                .ToList();
        }
        else
        {
            candidates = new List<GridPosition>();
            for (var z = zMin; z <= zMax; z++)
                for (var x = 0; x < gridWidth; x++)
                    if (IsEligible(x, z, walkable, occupied))
                        candidates.Add(new GridPosition(x, z));
        }

        // Seeded Fisher-Yates shuffle.
        var rng = Mulberry32(seed);
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = (int)Math.Floor(rng() * (i + 1));
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        var picked = candidates.Take(count).ToList();
        if (picked.Count >= count) return picked;

        // BFS fallback.
        var claimed = new HashSet<string>(picked.Select(p => $"{p.X},{p.Y}"));
        var centerX = gridWidth / 2;
        var centerZ = (zMin + zMax) / 2;
        var visited = new HashSet<string>();
        var queue = new Queue<GridPosition>();
        queue.Enqueue(new GridPosition(centerX, centerZ));

        while (queue.Count > 0 && picked.Count < count)
        {
            var cur = queue.Dequeue();
            var key = $"{cur.X},{cur.Y}";
            if (visited.Contains(key)) continue;
            visited.Add(key);

            if (cur.X < 0 || cur.X >= gridWidth || cur.Y < 0 || cur.Y >= gridHeight) continue;

            if (IsEligible(cur.X, cur.Y, walkable, occupied) && !claimed.Contains(key))
            {
                picked.Add(cur);
                claimed.Add(key);
            }

            // Neighbours: right, left, down, up (matching front's [[1,0],[-1,0],[0,1],[0,-1]]).
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = new GridPosition(cur.X + dx, cur.Y + dz);
                if (!visited.Contains($"{next.X},{next.Y}"))
                    queue.Enqueue(next);
            }
        }

        return picked;
    }

    // --- helpers ---

    private static void PropagateAffectedCells(JsonElement assetEl, Dictionary<string, bool> walkable, bool nonWalkable)
    {
        if (!assetEl.TryGetProperty("affectedCells", out var affectedEl) || affectedEl.ValueKind != JsonValueKind.Array)
            return;

        foreach (var cell in affectedEl.EnumerateArray())
        {
            if (!cell.TryGetProperty("x", out var xEl) || !xEl.TryGetInt32(out var ax)) continue;
            if (!cell.TryGetProperty("z", out var zEl) || !zEl.TryGetInt32(out var az)) continue;
            var key = $"{ax},{az}";
            if (nonWalkable)
                walkable[key] = false;
            // Non-blocking: no change to walkability.
        }
    }

    private static bool IsEligible(int x, int z, Dictionary<string, bool> walkable, ISet<string> occupied)
    {
        var key = $"{x},{z}";
        return walkable.TryGetValue(key, out var w) && w && !occupied.Contains(key);
    }

    private static GridPosition? ParseKey(string key)
    {
        var parts = key.Split(',');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], out var x)) return null;
        if (!int.TryParse(parts[1], out var z)) return null;
        return new GridPosition(x, z);
    }

    private static Func<double> Mulberry32(int seed)
    {
        uint a = (uint)seed;
        return () =>
        {
            a = unchecked(a + 0x6D2B79F5u);
            uint t = a;
            t = unchecked((uint)((long)(t ^ (t >> 15)) * (long)(t | 1u)));
            t ^= unchecked((uint)((long)t + (long)((uint)((long)(t ^ (t >> 7)) * (long)(t | 61u)))));
            return ((double)((t ^ (t >> 14)) & 0xFFFFFFFFu)) / 4294967296.0;
        };
    }
}
