using System.Collections.Concurrent;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

/// <summary>
/// Minimal per-session unit position store for validating collisions server-side.
/// This is intentionally lightweight (Free Roam first) and can be replaced by a full combat state later.
/// </summary>
public class SessionUnitPositionStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, GridPosition>> _positionsBySession = new();

    public ConcurrentDictionary<string, GridPosition> GetSessionPositions(string sessionId)
    {
        return _positionsBySession.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, GridPosition>(StringComparer.OrdinalIgnoreCase));
    }

    public bool TryGetPosition(string sessionId, string unitId, out GridPosition? pos)
    {
        pos = null;
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(unitId)) return false;
        var dict = GetSessionPositions(sessionId);
        if (!dict.TryGetValue(unitId, out var p)) return false;
        pos = p;
        return true;
    }

    public void SetPosition(string sessionId, string unitId, GridPosition pos)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(unitId)) return;
        var dict = GetSessionPositions(sessionId);
        dict[unitId] = pos;
    }

    public void RemoveSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        _positionsBySession.TryRemove(sessionId, out _);
    }
}

