namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// Persisted record of a successful dice roll submission. Written by
/// <c>GameHub.SubmitRollResult</c> after the in-memory pending-roll bookkeeping
/// completes, and consumed later by the campaign journal endpoint so DM/players
/// can review past rolls outside the live session window.
/// </summary>
public class RollHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Campaign the session belonged to. Indexed for journal queries.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>Live session identifier the roll happened in. Indexed for session-scoped views.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Pending-roll request id (matches <c>PendingRollRequest.RequestId</c>).</summary>
    public Guid RequestId { get; set; }

    /// <summary>Player who submitted the value.</summary>
    public Guid UserId { get; set; }

    /// <summary>Snapshot of the player's display name at submission time. May be null when unknown.</summary>
    public string? UserName { get; set; }

    /// <summary>Dice type label, e.g. "d20". Future-proofs the schema for non-d20 rolls.</summary>
    public string DiceType { get; set; } = "d20";

    /// <summary>Rolled face value (server-pre-rolled in the current flow).</summary>
    public int Value { get; set; }

    /// <summary>Optional DM-supplied label (e.g. "Perception", "Save vs poison").</summary>
    public string? Label { get; set; }

    public DateTime RolledAt { get; set; } = DateTime.UtcNow;
}
