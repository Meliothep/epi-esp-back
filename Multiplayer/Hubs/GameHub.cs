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
    private readonly StateManager _stateManager;
    private readonly IGameActionValidator _validator;

    /// <summary>
    /// Constructeur du GameHub
    /// </summary>
    public GameHub(ILogger<GameHub> logger, SessionManager sessionManager, MessageSequencer messageSequencer,
        StateManager stateManager, IGameActionValidator validator)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _messageSequencer = messageSequencer;
        _stateManager = stateManager;
        _validator = validator;
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
    /// Kick un joueur de la session (DM uniquement).
    /// </summary>
    /// <param name="targetUserId">ID de l'utilisateur à expulser.</param>
    public async Task<KickResult> KickPlayer(Guid targetUserId)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
        {
            return KickResult.Fail("Not in a session");
        }

        var userId = GetUserId();
        var result = _sessionManager.KickPlayer(sessionId, userId, targetUserId);

        if (result.Success && !string.IsNullOrEmpty(result.KickedConnectionId))
        {
            await Groups.RemoveFromGroupAsync(result.KickedConnectionId, sessionId);

            var kickPayload = new
            {
                userId = targetUserId,
                kickedBy = userId,
                reason = "Kicked by DM",
                timestamp = DateTime.UtcNow
            };
            await Clients.Group(sessionId).SendAsync("PlayerKicked", kickPayload);
            await Clients.Client(result.KickedConnectionId).SendAsync("PlayerKicked", kickPayload);
            _logger.LogInformation("User {TargetUserId} kicked from session {SessionId}", targetUserId, sessionId);
        }

        return result;
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
    /// Déplacement autorisé par le serveur. Valide puis diffuse MoveResult à la session.
    /// </summary>
    public async Task<MoveResult> Move(MoveRequest request)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        var validation = _validator.ValidateMove(session, request, userId);
        if (!validation.IsValid)
        {
            return new MoveResult
            {
                UnitId = request.UnitId,
                Success = false,
                Error = validation.ErrorMessage ?? validation.ErrorCode
            };
        }

        var result = new MoveResult
        {
            UnitId = request.UnitId,
            Path = request.Path ?? new List<GridPosition>(),
            ApCost = 1,
            Success = true
        };

        var message = _messageSequencer.CreateMessage(sessionId, "UnitMoved", result);
        await Clients.Group(sessionId).SendAsync("UnitMoved", message);
        return result;
    }

    /// <summary>
    /// Attaque autorisée par le serveur. Valide puis diffuse AttackResult à la session.
    /// </summary>
    public async Task<AttackResult> Attack(AttackRequest request)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        var validation = _validator.ValidateAttack(session, request, userId);
        if (!validation.IsValid)
        {
            return new AttackResult
            {
                AttackerId = request.AttackerId,
                TargetId = request.TargetId,
                AbilityId = request.AbilityId,
                Success = false,
                Error = validation.ErrorMessage ?? validation.ErrorCode
            };
        }

        var result = new AttackResult
        {
            AttackerId = request.AttackerId,
            TargetId = request.TargetId,
            AbilityId = request.AbilityId,
            DiceRoll = 0,
            Modifier = 0,
            Hit = true,
            Damage = null,
            Success = true
        };

        var message = _messageSequencer.CreateMessage(sessionId, "AttackResolved", result);
        await Clients.Group(sessionId).SendAsync("AttackResolved", message);
        return result;
    }

    /// <summary>
    /// Utilisation de capacité autorisée par le serveur. Valide puis diffuse UseAbilityResult à la session.
    /// </summary>
    public async Task<UseAbilityResult> UseAbility(UseAbilityRequest request)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        var validation = _validator.ValidateAbility(session, request, userId);
        if (!validation.IsValid)
        {
            return new UseAbilityResult
            {
                UnitId = request.UnitId,
                AbilityId = request.AbilityId,
                Success = false,
                Error = validation.ErrorMessage ?? validation.ErrorCode
            };
        }

        var result = new UseAbilityResult
        {
            UnitId = request.UnitId,
            AbilityId = request.AbilityId,
            Success = true
        };

        var message = _messageSequencer.CreateMessage(sessionId, "AbilityUsed", result);
        await Clients.Group(sessionId).SendAsync("AbilityUsed", message);
        return result;
    }

    /// <summary>
    /// Terminer le tour en cours. Valide puis diffuse TurnEnded à la session.
    /// </summary>
    public async Task EndTurn(TurnEndedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        var validation = _validator.ValidateTurnEnd(session, userId);
        if (!validation.IsValid)
            throw new HubException(validation.ErrorMessage ?? validation.ErrorCode ?? "Cannot end turn");

        var message = _messageSequencer.CreateMessage(sessionId, "TurnEnded", payload);
        await Clients.Group(sessionId).SendAsync("TurnEnded", message);
    }

    /// <summary>
    /// Envoyer un mouvement d'unité (legacy broadcast sans validation serveur).
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
    /// Envoyer un snapshot complet de l'état du jeu (stocké côté serveur pour RequestFullState). E2.3.
    /// </summary>
    public async Task SendGameStateSnapshot(GameStateSnapshot snapshot)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        _stateManager.SetSnapshot(sessionId, snapshot);

        var message = _messageSequencer.CreateMessage(sessionId, "GameStateSnapshot", snapshot);
        _logger.LogDebug("Game state snapshot stored and sent to session {SessionId}", sessionId);
        await Clients.Caller.SendAsync("GameStateSnapshot", message);
    }

    /// <summary>
    /// Requête de l'état complet du jeu pour la session en cours (e.g. on reconnect ou late join).
    /// Retourne le dernier snapshot stocké par SendGameStateSnapshot, ou un snapshot vide si aucun.
    /// </summary>
    public async Task<GameMessage<GameStateSnapshot>> RequestFullState()
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var snapshot = _stateManager.GetSnapshot(sessionId)
            ?? new GameStateSnapshot { SessionId = sessionId };

        var message = _messageSequencer.CreateMessage(sessionId, "FullStateSync", snapshot);
        await Clients.Caller.SendAsync("FullStateSync", message);
        return message;
    }

    #endregion
}