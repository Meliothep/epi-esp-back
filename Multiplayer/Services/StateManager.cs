using System.Collections.Concurrent;
using Multiplayer.Models.Messages;

namespace Multiplayer.Services;

/// <summary>
/// E2.3: Stores last game state snapshot per session (for RequestFullState and server-authoritative sync).
/// </summary>
public class StateManager
{
    private readonly ConcurrentDictionary<string, GameStateSnapshot> _sessionSnapshots = new();

    /// <summary>Stocke ou met à jour le dernier snapshot pour une session (appelé lorsque le client envoie SendGameStateSnapshot).</summary>
    public void SetSnapshot(string sessionId, GameStateSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        snapshot.SessionId = sessionId;
        _sessionSnapshots.AddOrUpdate(sessionId, snapshot, (_, _) => snapshot);
    }

    /// <summary>Récupère le dernier snapshot stocké pour une session, ou null.</summary>
    public GameStateSnapshot? GetSnapshot(string sessionId)
    {
        return _sessionSnapshots.TryGetValue(sessionId, out var s) ? s : null;
    }

    /// <summary>Supprime le snapshot lorsque la session est supprimée.</summary>
    public void RemoveSnapshot(string sessionId)
    {
        _sessionSnapshots.TryRemove(sessionId, out _);
    }
}
