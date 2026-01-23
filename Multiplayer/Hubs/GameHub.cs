using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Multiplayer.Models;
using Multiplayer.Models.Messages;
using Multiplayer.Services;
using System.Security.Claims;

namespace Multiplayer.Hubs;

[Authorize]
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly SessionManager _sessionManager;
    private readonly MessageSequencer _messageSequencer;

    /// <summary>
    /// Constructeur du GameHub
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="sessionManager"></param>
    /// <param name="messageSequencer"></param>
    public GameHub(ILogger<GameHub> logger, SessionManager sessionManager, MessageSequencer messageSequencer)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _messageSequencer = messageSequencer;
    }

    /// <summary>
    /// Gérer la connexion d'un joueur
    /// </summary>
    /// <returns></returns>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var userName = GetUserName();

        _logger.LogInformation(
            "User {UserId} ({UserName}) connected with ConnectionId {ConnectionId}",
            userId, userName, Context.ConnectionId
        );

        await Clients.Caller.SendAsync("Connected", new
        {
            connectionId = Context.ConnectionId,
            userId = userId,
            userName = userName,
            timestamp = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Gérer la déconnexion d'un joueur
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);

        _logger.LogInformation(
            "User {UserId} disconnected. ConnectionId: {ConnectionId}. Exception: {Exception}",
            userId, Context.ConnectionId, exception?.Message ?? "None"
        );

        if (sessionId != null)
        {
            // Marquer comme déconnecté (grace period pour reconnexion)
            _sessionManager.MarkPlayerDisconnected(Context.ConnectionId);

            // Notifier les autres joueurs
            await Clients.Group(sessionId).SendAsync("PlayerDisconnected", new
            {
                userId = userId,
                connectionId = Context.ConnectionId,
                timestamp = DateTime.UtcNow
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Créer une nouvelle session de jeu
    /// </summary>
    /// <param name="campaignId"></param>
    /// <returns></returns>
    public async Task<SessionInfo> CreateSession(Guid campaignId)
    {
        var userId = GetUserId();
        var userName = GetUserName();

        var session = _sessionManager.CreateSession(campaignId, userId, userName);

        // Rejoindre le groupe SignalR
        await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId);

        _logger.LogInformation("Session {SessionId} created by {UserId}", session.SessionId, userId);

        return MapToSessionInfo(session);
    }

    /// <summary>
    /// Joindre une session existante
    /// </summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public async Task<JoinResult> JoinSession(string sessionId)
    {
        var userId = GetUserId();
        var userName = GetUserName();

        var result = _sessionManager.JoinSession(sessionId, userId, userName, Context.ConnectionId);

        if (result.Success && result.Session != null)
        {
            // Rejoindre le groupe SignalR
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

            // Notifier les autres joueurs
            await Clients.OthersInGroup(sessionId).SendAsync("PlayerJoined", new
            {
                userId,
                userName,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
        }

        return result;
    }

    /// <summary>
    /// Quitter la session en cours
    /// </summary>
    /// <returns></returns>
    public async Task LeaveSession()
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
        {
            return;
        }

        var userId = GetUserId();

        // Retirer du groupe SignalR
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);

        // Retirer de la session
        _sessionManager.LeaveSession(Context.ConnectionId);

        // Notifier les autres joueurs
        await Clients.Group(sessionId).SendAsync("PlayerLeft", new
        {
            userId = userId,
            reason = "Player left",
            timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("User {UserId} left session {SessionId}", userId, sessionId);
    }

    /// <summary>
    /// Ping-Pong pour vérifier la connectivité
    /// </summary>
    /// <returns></returns>
    public async Task Ping()
    {
        _logger.LogDebug("Ping received from {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
                          ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new HubException("Invalid or missing user ID in token");
        }

        return userId;
    }

    private string GetUserName()
    {
        return Context.User?.FindFirst("name")?.Value
               ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
               ?? "Unknown";
    }

    private SessionInfo MapToSessionInfo(GameSession session)
    {
        return new SessionInfo
        {
            SessionId = session.SessionId,
            CampaignId = session.CampaignId,
            PlayerCount = session.Players.Count,
            MaxPlayers = session.MaxPlayers,
            State = session.State,
            Players = session.Players.Select(p => new PlayerInfo
            {
                UserId = p.UserId,
                UserName = p.UserName ?? "Inconnu",
                Role = p.Role,
                Status = p.Status
            }).ToList()
        };
    }

    #region [== Messages de jeu ==]

    /// <summary>
    /// Envoyer un mouvement d'unité
    /// <paramref name="payload"/>
    /// </summary>
    public async Task SendUnitMove(UnitMovedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
        {
            throw new HubException("Not in a session");
        }

        var message = _messageSequencer.CreateMessage(sessionId, "UnitMoved", payload);

        _logger.LogDebug("Unit {UnitId} moved in session {SessionId}", payload.UnitId, sessionId);

        // Broadcaster à tous les joueurs de la session
        await Clients.Group(sessionId).SendAsync("UnitMoved", message);
    }

    /// <summary>
    /// Envoyer l'utilisation d'une capacité
    /// <paramref name="payload"/>
    /// </summary>
    public async Task SendAbilityUsed(AbilityUsedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
        {
            throw new HubException("Not in a session");
        }

        var message = _messageSequencer.CreateMessage(sessionId, "AbilityUsed", payload);

        _logger.LogDebug("Ability {AbilityId} used by unit {UnitId} in session {SessionId}",
            payload.AbilityId, payload.UnitId, sessionId);

        await Clients.Group(sessionId).SendAsync("AbilityUsed", message);
    }

    /// <summary>
    /// Terminer le tour d'une unité
    /// <paramref name="payload"/>
    /// </summary>
    public async Task SendEndTurn(TurnEndedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
        {
            throw new HubException("Not in a session");
        }

        var message = _messageSequencer.CreateMessage(sessionId, "TurnEnded", payload);

        _logger.LogDebug("Turn ended for unit {UnitId} in session {SessionId}",
            payload.UnitId, sessionId);

        await Clients.Group(sessionId).SendAsync("TurnEnded", message);
    }

    /// <summary>
    /// Envoyer un snapshot complet de l'état du jeu
    /// <paramref name="snapshot"/>
    /// </summary>
    public async Task SendGameStateSnapshot(GameStateSnapshot snapshot)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
        {
            throw new HubException("Not in a session");
        }

        var message = _messageSequencer.CreateMessage(sessionId, "GameStateSnapshot", snapshot);

        _logger.LogInformation("Game state snapshot sent to session {SessionId}", sessionId);

        await Clients.Caller.SendAsync("GameStateSnapshot", message);
    }

    #endregion
}