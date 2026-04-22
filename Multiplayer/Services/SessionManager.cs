using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Multiplayer.Models;
using static Multiplayer.Define;

namespace Multiplayer.Services;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToSession = new(); // ConnectionId -> SessionId
    private readonly ConcurrentDictionary<string, string> _joinCodeToSession = new(); // JoinCode -> SessionId
    private readonly ILogger<SessionManager> _logger;
    private readonly StateManager _stateManager;
    private readonly MessageSequencer _messageSequencer;

    public SessionManager(ILogger<SessionManager> logger, StateManager stateManager, MessageSequencer messageSequencer)
    {
        _logger = logger;
        _stateManager = stateManager;
        _messageSequencer = messageSequencer;
    }

    /// <summary>
    /// Créer une nouvelle session de jeu
    /// </summary>
    /// <param name="campaignId"></param>
    /// <param name="dmUserId"></param>
    /// <param name="dmUserName"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public GameSession CreateSession(Guid campaignId, Guid dmUserId, string dmUserName)
    {
        var sessionId = GenerateSessionId();
        var joinCode = GenerateRoomCode();
        var session = new GameSession
        {
            SessionId = sessionId,
            JoinCode = joinCode,
            CampaignId = campaignId,
            DmUserId = dmUserId,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            State = SessionState.Lobby
        };

        // DM = premier joueur
        session.Players.Add(new SessionPlayer
        {
            UserId = dmUserId,
            UserName = dmUserName,
            Role = PlayerRole.DungeonMaster,
            Status = ConnectionStatus.Connected,
            JoinedAt = DateTime.UtcNow
        });

        if (_sessions.TryAdd(sessionId, session))
        {
            _joinCodeToSession.TryAdd(joinCode, sessionId);
            _logger.LogInformation("Session {SessionId} created for campaign {CampaignId} by DM {DmUserId}",
                sessionId, campaignId, dmUserId);
            return session;
        }

        throw new InvalidOperationException("Failed to create session");
    }

    /// <summary>
    /// Joindre une session existante
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="userId"></param>
    /// <param name="userName"></param>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    public JoinResult JoinSession(string sessionId, Guid userId, string userName, string connectionId)
    {
        // Allow joining by joinCode (XXXX-XXXX) or by internal sessionId.
        var resolvedSessionId = sessionId;
        if (!_sessions.ContainsKey(resolvedSessionId) &&
            _joinCodeToSession.TryGetValue(sessionId, out var mappedSessionId))
        {
            resolvedSessionId = mappedSessionId;
        }

        if (!_sessions.TryGetValue(resolvedSessionId, out var session))
        {
            return JoinResult.Fail("Session not found");
        }

        lock (session.Players)
        {
            // Vérifier si le joueur est déjà dans la session (reconnexion) 
            var existingPlayer = session.Players.FirstOrDefault(p => p.UserId == userId);
            if (existingPlayer != null)
            {
                existingPlayer.ConnectionId = connectionId;
                existingPlayer.Status = ConnectionStatus.Connected;
                existingPlayer.DisconnectedAt = null;
                if (existingPlayer.Role == PlayerRole.DungeonMaster)
                    session.DmDisconnectedAt = null;

                _connectionToSession.TryAdd(connectionId, resolvedSessionId);

                _logger.LogInformation("User {UserId} reconnected to session {SessionId}",
                    userId, resolvedSessionId);

                return JoinResult.Ok(session, existingPlayer);
            }

            // Vérifier la limite de joueurs (max: 6)
            if (session.Players.Count >= session.MaxPlayers)
            {
                return JoinResult.Fail("Session is full");
            }

            // Ajouter le nouveau joueur
            var newPlayer = new SessionPlayer
            {
                UserId = userId,
                UserName = userName,
                ConnectionId = connectionId,
                Role = PlayerRole.Player,
                Status = ConnectionStatus.Connected,
                JoinedAt = DateTime.UtcNow
            };

            session.Players.Add(newPlayer);
            session.LastActivityAt = DateTime.UtcNow;

            _connectionToSession.TryAdd(connectionId, resolvedSessionId);

            _logger.LogInformation("User {UserId} ({UserName}) joined session {SessionId}",
                userId, userName, resolvedSessionId);

            return JoinResult.Ok(session, newPlayer);
        }
    }

    /// <summary>
    /// Quitter une session
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    public bool LeaveSession(string connectionId)
    {
        if (!_connectionToSession.TryRemove(connectionId, out var sessionId))
        {
            return false;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }

        lock (session.Players)
        {
            var player = session.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player != null)
            {
                session.Players.Remove(player);
                session.LastActivityAt = DateTime.UtcNow;

                _logger.LogInformation("User {UserId} left session {SessionId}",
                    player.UserId, sessionId);

                // Si plus de joueurs, supprimer la session
                if (session.Players.Count == 0)
                {
                    _sessions.TryRemove(sessionId, out _);
                    if (!string.IsNullOrWhiteSpace(session.JoinCode))
                        _joinCodeToSession.TryRemove(session.JoinCode, out _);
                    _stateManager.RemoveSnapshot(sessionId);
                    _messageSequencer.ResetSequence(sessionId);
                    _logger.LogInformation("Session {SessionId} removed (no players left)", sessionId);
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Marquer un joueur comme déconnecté
    /// </summary>
    /// <param name="connectionId"></param>
    public void MarkPlayerDisconnected(string connectionId)
    {
        if (_connectionToSession.TryGetValue(connectionId, out var sessionId) &&
            _sessions.TryGetValue(sessionId, out var session))
        {
            lock (session.Players)
            {
                var player = session.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (player != null)
                {
                    player.Status = ConnectionStatus.Disconnected;
                    player.DisconnectedAt = DateTime.UtcNow;
                    var wasDm = player.Role == PlayerRole.DungeonMaster;
                    player.ConnectionId = null;
                    if (wasDm)
                        session.DmDisconnectedAt = DateTime.UtcNow;

                    _logger.LogInformation("User {UserId} marked as disconnected in session {SessionId}",
                        player.UserId, sessionId);
                }
            }
        }

        _connectionToSession.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Kick un joueur de la session (DM uniquement).
    /// </summary>
    public KickResult KickPlayer(string sessionId, Guid kickerUserId, Guid targetUserId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return KickResult.Fail("Session not found");

        lock (session.Players)
        {
            var dm = session.Players.FirstOrDefault(p => p.Role == PlayerRole.DungeonMaster);
            if (dm == null || dm.UserId != kickerUserId)
                return KickResult.Fail("Only the DM can kick players");

            if (targetUserId == kickerUserId)
                return KickResult.Fail("Cannot kick yourself");

            var target = session.Players.FirstOrDefault(p => p.UserId == targetUserId);
            if (target == null)
                return KickResult.Fail("Player not in session");

            var connectionId = target.ConnectionId;
            session.Players.Remove(target);
            session.LastActivityAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(connectionId))
                _connectionToSession.TryRemove(connectionId, out _);

            _logger.LogInformation("User {TargetUserId} kicked from session {SessionId} by DM {KickerUserId}",
                targetUserId, sessionId, kickerUserId);

            if (session.Players.Count == 0)
            {
                _sessions.TryRemove(sessionId, out _);
                if (!string.IsNullOrWhiteSpace(session.JoinCode))
                    _joinCodeToSession.TryRemove(session.JoinCode, out _);
                _stateManager.RemoveSnapshot(sessionId);
                _messageSequencer.ResetSequence(sessionId);
                _logger.LogInformation("Session {SessionId} removed (no players left after kick)", sessionId);
            }

            return KickResult.Ok(connectionId ?? string.Empty);
        }
    }

    /// <summary>
    /// Récupérer une session par son ID
    /// </summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public GameSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Récupérer l'ID de session associé à une connexion
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    public string? GetSessionByConnection(string connectionId)
    {
        _connectionToSession.TryGetValue(connectionId, out var sessionId);
        // Touch the session's activity timestamp whenever it's resolved through
        // the hub so the background cleanup can't evict a session mid-combat.
        // Every hub command method calls this at entry, so this becomes the one
        // central place that marks the session as "live".
        if (sessionId != null && _sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivityAt = DateTime.UtcNow;
        }
        return sessionId;
    }

    /// <summary>
    /// Récupérer toutes les sessions pour une campagne donnée
    /// </summary>
    /// <param name="campaignId"></param>
    /// <returns></returns>
    public List<GameSession> GetSessionsByCampaign(Guid campaignId)
    {
        return _sessions.Values
            .Where(s => s.CampaignId == campaignId)
            .ToList();
    }

    /// <summary>
    /// Find the active session this user is a member of (connected or disconnected).
    /// Used by the hub's reconnect path so a refreshed client can be re-placed
    /// into its previous session without replaying the invite/join flow.
    /// Returns null when the user has no active session.
    /// </summary>
    public GameSession? FindSessionByUser(Guid userId)
    {
        foreach (var session in _sessions.Values)
        {
            lock (session.Players)
            {
                if (session.Players.Any(p => p.UserId == userId))
                    return session;
            }
        }
        return null;
    }

    /// <summary>
    /// Créer une room standalone (sans campagne) pour le mode multijoueur libre.
    /// </summary>
    public GameSession CreateRoom(Guid hostUserId, string hostUserName, int maxPlayers)
    {
        if (maxPlayers < 2 || maxPlayers > 6)
            throw new ArgumentException("maxPlayers must be between 2 and 6");

        var sessionId = GenerateRoomCode();
        var joinCode = sessionId; // For rooms, SessionId is already a short joinable code.
        var session = new GameSession
        {
            SessionId = sessionId,
            JoinCode = joinCode,
            CampaignId = null,
            DmUserId = hostUserId,
            MaxPlayers = maxPlayers,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            State = SessionState.Lobby
        };

        session.Players.Add(new SessionPlayer
        {
            UserId = hostUserId,
            UserName = hostUserName,
            Role = PlayerRole.DungeonMaster,
            Status = ConnectionStatus.Connected,
            JoinedAt = DateTime.UtcNow
        });

        if (_sessions.TryAdd(sessionId, session))
        {
            _joinCodeToSession.TryAdd(joinCode, sessionId);
            _logger.LogInformation("Room {SessionId} created by host {HostUserId} (max {MaxPlayers} players)",
                sessionId, hostUserId, maxPlayers);
            return session;
        }

        throw new InvalidOperationException("Failed to create room");
    }

    /// <summary>
    /// Définir le personnage sélectionné par un joueur dans la session.
    /// Choosing a real character clears any previously-picked default template.
    /// </summary>
    public bool SetPlayerCharacter(string sessionId, Guid userId, Guid? characterId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        lock (session.Players)
        {
            var player = session.Players.FirstOrDefault(p => p.UserId == userId);
            if (player == null)
                return false;

            player.SelectedCharacterId = characterId;
            if (characterId.HasValue) player.SelectedDefaultTemplate = null;
            session.LastActivityAt = DateTime.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Pick a preset template (warrior / mage / archer) as a no-persisted-character
    /// quickstart. Mutually exclusive with a real SelectedCharacterId.
    /// </summary>
    public bool SetPlayerDefaultTemplate(string sessionId, Guid userId, string? templateId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        lock (session.Players)
        {
            var player = session.Players.FirstOrDefault(p => p.UserId == userId);
            if (player == null)
                return false;

            player.SelectedDefaultTemplate = string.IsNullOrWhiteSpace(templateId) ? null : templateId;
            if (!string.IsNullOrWhiteSpace(templateId)) player.SelectedCharacterId = null;
            session.LastActivityAt = DateTime.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Définir la carte de la session.
    /// </summary>
    public void SetSessionMapId(string sessionId, string mapId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.MapId = mapId;
            session.LastActivityAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Générer un ID de session unique
    /// </summary>
    /// <returns></returns>
    private string GenerateSessionId()
    {
        return $"session_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Générer un code de room court (XXXX-XXXX).
    /// </summary>
    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sans I, O, 0, 1 pour éviter l'ambiguïté
        var random = Random.Shared;
        var code = new char[9]; // 8 chars + 1 dash
        for (int i = 0; i < 9; i++)
        {
            if (i == 4) { code[i] = '-'; continue; }
            var idx = i > 4 ? i - 1 : i;
            code[i] = chars[random.Next(chars.Length)];
        }
        var roomCode = new string(code);
        // Ensure uniqueness
        if (_sessions.ContainsKey(roomCode))
            return GenerateRoomCode();
        return roomCode;
    }

    /// <summary>
    /// Nettoyer les sessions inactives
    /// </summary>
    /// <param name="inactivityThreshold"></param>
    /// <returns></returns>
    public int CleanupStaleSessions(TimeSpan inactivityThreshold)
    {
        var cutoffTime = DateTime.UtcNow - inactivityThreshold;
        var staleSessions = _sessions.Values
            .Where(s => s.LastActivityAt < cutoffTime)
            .ToList();

        foreach (var session in staleSessions)
        {
            _sessions.TryRemove(session.SessionId, out _);
            if (!string.IsNullOrWhiteSpace(session.JoinCode))
                _joinCodeToSession.TryRemove(session.JoinCode, out _);
            _stateManager.RemoveSnapshot(session.SessionId);
            _messageSequencer.ResetSequence(session.SessionId);
            foreach (var p in session.Players.Where(p => !string.IsNullOrEmpty(p.ConnectionId)))
                _connectionToSession.TryRemove(p.ConnectionId!, out _);
            _logger.LogInformation("Removed stale session {SessionId} (inactive since {LastActivity})",
                session.SessionId, session.LastActivityAt);
        }

        return staleSessions.Count;
    }

    /// <summary>
        /// Nettoyer les sessions où le DM a été déconnecté pour plus de dmDisconnectedThreshold,
        /// ou la session a été inactive pour plus de inactivityThreshold.
    /// </summary>
    /// <param name="dmDisconnectedThreshold">e.g. 5 minutes - session supprimée si le DM est toujours déconnecté.</param>
    /// <param name="inactivityThreshold">e.g. 10 minutes - session supprimée si aucune activité.</param>
    /// <returns>Nombre de sessions supprimées.</returns>
    public int CleanupStaleSessionsByPolicy(TimeSpan dmDisconnectedThreshold, TimeSpan inactivityThreshold)
    {
        var now = DateTime.UtcNow;
        var toRemove = _sessions.Values
            .Where(s =>
                // Never evict a session while any player is currently connected —
                // the LastActivityAt heuristic isn't enough when combat commands
                // don't always touch it. An active websocket is the authoritative
                // "still alive" signal.
                s.Players.All(p => p.Status != Define.ConnectionStatus.Connected) &&
                ((s.DmDisconnectedAt.HasValue && (now - s.DmDisconnectedAt.Value) >= dmDisconnectedThreshold) ||
                 (s.LastActivityAt < now - inactivityThreshold)))
            .ToList();

        foreach (var session in toRemove)
        {
            _sessions.TryRemove(session.SessionId, out _);
            if (!string.IsNullOrWhiteSpace(session.JoinCode))
                _joinCodeToSession.TryRemove(session.JoinCode, out _);
            _stateManager.RemoveSnapshot(session.SessionId);
            _messageSequencer.ResetSequence(session.SessionId);
            foreach (var p in session.Players.Where(p => !string.IsNullOrEmpty(p.ConnectionId)))
                _connectionToSession.TryRemove(p.ConnectionId!, out _);
            _logger.LogInformation(
                "Removed session {SessionId} (DM disconnected: {DmDisconnected}, LastActivity: {LastActivity})",
                session.SessionId, session.DmDisconnectedAt, session.LastActivityAt);
        }

        return toRemove.Count;
    }
}