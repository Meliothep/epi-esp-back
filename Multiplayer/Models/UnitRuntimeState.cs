using System;
using static Multiplayer.Define;

namespace Multiplayer.Models
{
    public class UnitRuntimeState
    {
        public string UnitId { get; set; } = string.Empty;
        public Guid? OwnerUserId { get; set; }
        public UnitTeam Team { get; set; } = UnitTeam.Neutral;
        public string Name { get; set; } = string.Empty;
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int CurrentAp { get; set; }
        public int MaxAp { get; set; }
        public int Initiative { get; set; }
        public bool IsAlive => CurrentHp > 0;
    }
}
