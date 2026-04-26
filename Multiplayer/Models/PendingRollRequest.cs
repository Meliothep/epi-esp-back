namespace Multiplayer.Models;

public class PendingRollRequest
{
    private readonly object _lock = new();

    public Guid RequestId { get; init; }
    public string DiceType { get; init; } = "d20";
    public string? Label { get; init; }

    public IReadOnlyDictionary<Guid, int> RollValues { get; init; }
        = new Dictionary<Guid, int>();

    private readonly HashSet<Guid> _pendingUserIds = new();

    public IReadOnlySet<Guid> PendingUserIds
    {
        get => _pendingUserIds;
        init
        {
            _pendingUserIds.Clear();
            foreach (var id in value)
            {
                if (!RollValues.ContainsKey(id))
                    throw new ArgumentException(
                        $"PendingUserIds contained {id} which is not present in RollValues",
                        nameof(value));
                _pendingUserIds.Add(id);
            }
        }
    }

    private readonly Dictionary<Guid, int> _submittedValues = new();

    // Kept exclusively to support rejoin-snapshot replay for the DM.
    public IReadOnlyDictionary<Guid, int> SubmittedValues => _submittedValues;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool TrySubmit(Guid userId, out int value, out bool complete)
    {
        lock (_lock)
        {
            value = 0;
            complete = false;

            // Validate read BEFORE mutating so an unexpected missing RollValues
            // entry cannot leave the request in an inconsistent state.
            // Read into a local first so that if _pendingUserIds.Remove fails
            // (double-submit or stale pending set), the out `value` stays 0.
            if (!RollValues.TryGetValue(userId, out var rolled)) return false;
            if (!_pendingUserIds.Remove(userId)) return false;

            value = rolled;
            _submittedValues[userId] = value;
            complete = _pendingUserIds.Count == 0;
            return true;
        }
    }

    /// <summary>
    /// Returns a snapshot of submitted values, copied inside the lock so iteration
    /// is safe against concurrent TrySubmit calls.
    /// </summary>
    public IReadOnlyDictionary<Guid, int> SnapshotSubmittedValues()
    {
        lock (_lock)
        {
            return new Dictionary<Guid, int>(_submittedValues);
        }
    }
}
