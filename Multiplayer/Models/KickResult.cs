namespace Multiplayer.Models;

/// <summary>
/// Result of a KickPlayer operation (E2.2).
/// </summary>
public class KickResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? KickedConnectionId { get; set; }

    public static KickResult Ok(string kickedConnectionId)
        => new() { Success = true, KickedConnectionId = kickedConnectionId };

    public static KickResult Fail(string message)
        => new() { Success = false, Message = message };
}
