using System.Collections.Generic;
using System.Threading;
using static Multiplayer.Define;

namespace Multiplayer.Models
{
    public class CombatState
    {
        public CombatPhase Phase { get; set; } = CombatPhase.FreeRoam;
        public int Round { get; set; } = 0;
        public List<string> TurnOrder { get; set; } = new();
        public int CurrentUnitIndex { get; set; } = 0;
        public Dictionary<string, UnitRuntimeState> Units { get; set; } = new();
        public CombatResult? Outcome { get; set; }

        /// <summary>
        /// Per-session serialisation gate. CombatManager acquires this before
        /// mutating the state so overlapping hub invocations don't race.
        /// </summary>
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public string? CurrentUnitId => TurnOrder.Count == 0
            ? null
            : TurnOrder[CurrentUnitIndex % TurnOrder.Count];
    }
}
