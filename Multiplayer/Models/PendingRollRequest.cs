namespace Multiplayer.Models;

public class PendingRollRequest
{
    public Guid RequestId { get; init; }
    public string DiceType { get; init; } = "d20";
    public string? Label { get; init; }

    public IReadOnlyDictionary<Guid, int> RollValues { get; init; }
        = new Dictionary<Guid, int>();

    public HashSet<Guid> PendingUserIds { get; init; } = new();

    // Kept exclusively to support rejoin-snapshot replay for the DM.
    public Dictionary<Guid, int> SubmittedValues { get; } = new();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool TrySubmit(Guid userId, out int value, out bool complete)
    {
        lock (this)
        {
            value = 0;
            complete = false;

            // Validate read BEFORE mutating so an unexpected missing RollValues
            // entry cannot leave the request in an inconsistent state.
            // Read into a local first so that if PendingUserIds.Remove fails
            // (double-submit or stale pending set), the out `value` stays 0.
            if (!RollValues.TryGetValue(userId, out var rolled)) return false;
            if (!PendingUserIds.Remove(userId)) return false;

            value = rolled;
            SubmittedValues[userId] = value;
            complete = PendingUserIds.Count == 0;
            return true;
        }
    }
}
