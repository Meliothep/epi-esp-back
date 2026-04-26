namespace Multiplayer.Models.Messages;

/// <summary>
/// Payload when the DM force-moves a token on the board.
/// </summary>
public class DmMoveTokenPayload
{
    /// <summary>Unit ID to move (can be any player's unit).</summary>
    public string UnitId { get; set; } = string.Empty;

    /// <summary>Target grid position (backend uses x,y — frontend maps to x,z).</summary>
    public GridPosition Target { get; set; } = new();
}

/// <summary>
/// Result of a DM hidden dice roll (only sent back to the DM).
/// </summary>
public class DmHiddenRollPayload
{
    public int DiceType { get; set; } = 20;
    public int Result { get; set; }
    public int Modifier { get; set; }
    public int Total { get; set; }
    public string? Label { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DM grants an item to a player character via the SignalR hub. The hub persists
/// through IInventoryService and broadcasts ItemGranted for toasts.
/// </summary>
public class DmGrantItemPayload
{
    /// <summary>Target player's user ID (Guid).</summary>
    public Guid TargetUserId { get; set; }

    /// <summary>Item identifier from the catalogue (matches Item.Id).</summary>
    public Guid ItemId { get; set; }

    /// <summary>Display name of the item (used only for the toast).</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Quantity granted.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Optional description or flavour text for the toast.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Broadcast payload when a player receives an item from the DM — drives the
/// "X received Y from the DM" toast. Inventory state changes go via the
/// InventoryChanged event.
/// </summary>
public class ItemGrantedPayload
{
    public Guid TargetUserId { get; set; }
    public string TargetUserName { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DM spawns a new enemy unit on the board.
/// </summary>
public class DmSpawnUnitPayload
{
    /// <summary>Unique ID for the new unit (generated client-side).</summary>
    public string UnitId { get; set; } = string.Empty;

    /// <summary>Template key from the enemy catalogue (e.g. "skeleton_warrior").</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Display name of the unit.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unit type key (e.g. "enemy_skeleton").</summary>
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Target grid position.</summary>
    public GridPosition Target { get; set; } = new();

    /// <summary>Serialised unit stats (JSON).</summary>
    public string StatsJson { get; set; } = string.Empty;
}

/// <summary>
/// DM awards raw XP to a player character.
/// </summary>
public record DmAwardExperiencePayload
{
    public Guid TargetUserId { get; init; }
    public int ExperienceAmount { get; init; }
}

/// <summary>
/// DM forcibly triggers one or more level-ups for a player character.
/// </summary>
public record DmForceLevelUpPayload
{
    public Guid TargetUserId { get; init; }
    public int Levels { get; init; } = 1;
}

/// <summary>
/// DM grants (or removes) gold from a player character wallet.
/// </summary>
public record DmGrantGoldPayload
{
    public Guid TargetUserId { get; init; }
    public int Amount { get; init; }
    public CurrencyType CurrencyType { get; init; } = CurrencyType.Gp;
}

/// <summary>
/// Broadcast when DM-driven XP / level-up progression is applied.
/// </summary>
public record CharacterProgressedPayload
{
    public Guid TargetUserId { get; init; }
    public string TargetUserName { get; init; } = string.Empty;
    public Guid CharacterId { get; init; }
    public int AwardedExperience { get; init; }
    public int ExperienceRemainder { get; init; }
    public int PreviousLevel { get; init; }
    public int NewLevel { get; init; }
    public int LevelUps { get; init; }
    public int CurrentHitPoints { get; init; }
    public int MaxHitPoints { get; init; }
    public int ArmorClass { get; init; }
    public int Initiative { get; init; }
    public int Speed { get; init; }
    public int Strength { get; init; }
    public int Dexterity { get; init; }
    public int Constitution { get; init; }
    public int Intelligence { get; init; }
    public int Wisdom { get; init; }
    public int Charisma { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Broadcast when DM grants/removes gold.
/// </summary>
public record GoldGrantedPayload
{
    public Guid TargetUserId { get; init; }
    public string TargetUserName { get; init; } = string.Empty;
    public Guid CharacterId { get; init; }
    public int Amount { get; init; }
    public CurrencyType CurrencyType { get; init; } = CurrencyType.Gp;
    public int CopperPieces { get; init; }
    public int SilverPieces { get; init; }
    public int ElectrumPieces { get; init; }
    public int GoldPieces { get; init; }
    public int PlatinumPieces { get; init; }
    public int TotalInCopper { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
