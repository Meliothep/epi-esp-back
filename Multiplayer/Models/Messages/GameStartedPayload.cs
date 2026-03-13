namespace Multiplayer.Models.Messages;

public class GameStartedPayload
{
    public string MapId { get; set; } = string.Empty;
    public string? MapData { get; set; }
    public List<UnitAssignment> UnitAssignments { get; set; } = new();
}

public class UnitAssignment
{
    public Guid UserId { get; set; }
    public string UnitId { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string CharacterClass { get; set; } = string.Empty;
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public int ArmorClass { get; set; }
    public int Speed { get; set; }
    public int Initiative { get; set; }
    public int AttackDamage { get; set; }
    public int Defense { get; set; }
    public int MovementRange { get; set; }
    public int AttackRange { get; set; }
}
