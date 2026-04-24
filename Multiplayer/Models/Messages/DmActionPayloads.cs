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
public class DmAwardExperiencePayload
{
    public Guid TargetUserId { get; set; }
    public int ExperienceAmount { get; set; }
}

/// <summary>
/// DM forcibly triggers one or more level-ups for a player character.
/// </summary>
public class DmForceLevelUpPayload
{
    public Guid TargetUserId { get; set; }
    public int Levels { get; set; } = 1;
}

/// <summary>
/// DM grants (or removes) gold from a player character wallet.
/// </summary>
public class DmGrantGoldPayload
{
    public Guid TargetUserId { get; set; }
    public int Amount { get; set; }
    public string CurrencyType { get; set; } = "gp";
    public int GoldPieces { get; set; }
}

/// <summary>
/// Broadcast when DM-driven XP / level-up progression is applied.
/// </summary>
public class CharacterProgressedPayload
{
    public Guid TargetUserId { get; set; }
    public string TargetUserName { get; set; } = string.Empty;
    public Guid CharacterId { get; set; }
    public int AwardedExperience { get; set; }
    public int ExperienceRemainder { get; set; }
    public int PreviousLevel { get; set; }
    public int NewLevel { get; set; }
    public int LevelUps { get; set; }
    public int CurrentHitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int Initiative { get; set; }
    public int Speed { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Broadcast when DM grants/removes gold.
/// </summary>
public class GoldGrantedPayload
{
    public Guid TargetUserId { get; set; }
    public string TargetUserName { get; set; } = string.Empty;
    public Guid CharacterId { get; set; }
    public int Amount { get; set; }
    public string CurrencyType { get; set; } = "gp";
    public int GoldDelta { get; set; }
    public int CopperPieces { get; set; }
    public int SilverPieces { get; set; }
    public int ElectrumPieces { get; set; }
    public int GoldPieces { get; set; }
    public int PlatinumPieces { get; set; }
    public int TotalInCopper { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
