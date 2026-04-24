namespace Multiplayer.Models.Messages;

/// <summary>
/// Broadcast after <c>DmAdjustHp</c>: every client applies the new HP and
/// isAlive transition verbatim. <see cref="Delta"/> is the actual change after
/// clamping (e.g. a -50 delta on a unit with 20 HP becomes -20).
/// </summary>
public class UnitHpAdjustedPayload
{
    public string UnitId { get; set; } = string.Empty;
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public bool IsAlive { get; set; }
    public int Delta { get; set; }
    public bool WasAlive { get; set; }
}
