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
/// Request for the DM to grant an item to a player character.
/// Used both by the REST endpoint and the SignalR hub.
/// </summary>
public class DmGrantItemPayload
{
    /// <summary>Target player's user ID (Guid).</summary>
    public Guid TargetUserId { get; set; }

    /// <summary>Item identifier from the catalogue.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Display name of the item.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Quantity granted.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Optional description or flavour text.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Broadcast payload when a player receives an item from the DM.
/// </summary>
public class ItemGrantedPayload
{
    public Guid TargetUserId { get; set; }
    public string TargetUserName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
