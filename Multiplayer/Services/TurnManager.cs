using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.Models;

namespace Multiplayer.Services
{
    /// <summary>
    /// Pure initiative logic. Takes a snapshot of units, returns an ordered list of ids.
    /// Higher initiative goes first; ties break by a deterministic per-unit tiebreak (seeded
    /// hash of the unit id) so a given roster always produces the same order for the same seed.
    /// </summary>
    public class TurnManager
    {
        private readonly Random _random;

        public TurnManager() : this(null) { }

        public TurnManager(int? seed)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Roll initiative for every alive unit (d20 + unit.Initiative modifier) and return
        /// the resulting turn order, highest first. Dead units are excluded.
        /// </summary>
        public List<string> CalculateInitiative(IEnumerable<UnitRuntimeState> units)
        {
            return units
                .Where(u => u.IsAlive)
                .Select(u => new
                {
                    u.UnitId,
                    Total = _random.Next(1, 21) + u.Initiative,
                    Tiebreak = u.UnitId.GetHashCode()
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Tiebreak)
                .Select(x => x.UnitId)
                .ToList();
        }
    }
}
