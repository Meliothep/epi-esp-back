using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Multiplayer.Models;
using static Multiplayer.Define;

namespace Multiplayer.Services;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _connectionToSession = new(); // ConnectionId -> SessionId
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
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
        var session = new GameSession
        {
            SessionId = sessionId,
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
        if (!_sessions.TryGetValue(sessionId, out var session))
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

                _connectionToSession.TryAdd(connectionId, sessionId);

                _logger.LogInformation("User {UserId} reconnected to session {SessionId}",
                    userId, sessionId);

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

            _connectionToSession.TryAdd(connectionId, sessionId);

            _logger.LogInformation("User {UserId} ({UserName}) joined session {SessionId}",
                userId, userName, sessionId);

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
                    player.ConnectionId = null;

                    _logger.LogInformation("User {UserId} marked as disconnected in session {SessionId}",
                        player.UserId, sessionId);
                }
            }
        }

        _connectionToSession.TryRemove(connectionId, out _);
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
    /// Générer un ID de session unique
    /// </summary>
    /// <returns></returns>
    private string GenerateSessionId()
    {
        return $"session_{Guid.NewGuid():N}";
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
            _logger.LogInformation("Removed stale session {SessionId} (inactive since {LastActivity})",
                session.SessionId, session.LastActivityAt);
        }

        return staleSessions.Count;
    }
}