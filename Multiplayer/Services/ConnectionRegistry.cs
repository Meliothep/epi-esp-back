using System.Collections.Concurrent;

namespace Multiplayer.Services;

/// <summary>
/// Registre userId (Guid) → connexions SignalR actives, toutes pages confondues.
/// Nécessaire car Clients.User(...) route par snowflake Discord, pas par le Guid.
/// </summary>
public class ConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connections = new();

    public void Register(Guid userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        set.TryAdd(connectionId, 0);
    }

    public void Unregister(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set)) return;
        set.TryRemove(connectionId, out _);
        if (set.IsEmpty) _connections.TryRemove(userId, out _);
    }

    public IReadOnlyList<string> GetConnections(Guid userId)
    {
        return _connections.TryGetValue(userId, out var set)
            ? set.Keys.ToList()
            : Array.Empty<string>();
    }

    public bool IsConnected(Guid userId) =>
        _connections.TryGetValue(userId, out var set) && !set.IsEmpty;
}
