using System.Collections.Concurrent;
using Multiplayer.Models;

namespace Multiplayer.Services;

/// <summary>
/// Stockage en mémoire des snapshots par session.
/// Utilisé pour resynchroniser un client (RequestFullState).
/// </summary>
public class StateManager
{
    private readonly ConcurrentDictionary<string, GameStateSnapshot> _snapshots = new();

    public GameStateSnapshot? GetSnapshot(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        return _snapshots.TryGetValue(sessionId, out var s) ? s : null;
    }

    public void SetSnapshot(string sessionId, GameStateSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        _snapshots[sessionId] = snapshot;
    }

    public void ClearSnapshot(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        _snapshots.TryRemove(sessionId, out _);
    }
}

