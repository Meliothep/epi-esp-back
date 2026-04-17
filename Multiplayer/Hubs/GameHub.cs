using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Multiplayer.Models;
using Multiplayer.Models.Messages;
using Multiplayer.Services;
using System.Security.Claims;
using static Multiplayer.Define;

namespace Multiplayer.Hubs;

[Authorize]
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly SessionManager _sessionManager;
    private readonly MessageSequencer _messageSequencer;
    private readonly StateManager _stateManager;
    private readonly IGameActionValidator _validator;
    private readonly SessionUnitPositionStore _positions;
    private readonly IUserContextService _userContextService;
    private readonly ICharacterLookupService _characterLookup;

    /// <summary>
    /// Constructeur du GameHub
    /// </summary>
    public GameHub(ILogger<GameHub> logger, SessionManager sessionManager, MessageSequencer messageSequencer,
        StateManager stateManager, IGameActionValidator validator, SessionUnitPositionStore positions,
        IUserContextService userContextService, ICharacterLookupService characterLookup)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _messageSequencer = messageSequencer;
        _stateManager = stateManager;
        _validator = validator;
        _positions = positions;
        _userContextService = userContextService;
        _characterLookup = characterLookup;
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
    /// <param name="guildId">Optionnel: id de guild Discord (activité).</param>
    /// <param name="voiceChannelId">Optionnel: id de salon vocal Discord (activité).</param>
    /// <returns></returns>
    /// <remarks>
    /// Two distinct events are emitted to avoid double-notification for users subscribed to both groups:
    /// - <c>SessionStarted</c>: sent to the campaign group only, with no Discord context in the payload.
    /// - <c>ActivitySessionStarted</c>: sent to the activity group only (when guildId and voiceChannelId
    ///   are provided), payload includes guildId and voiceChannelId.
    /// </remarks>
    public async Task<SessionInfo> CreateSession(Guid campaignId, string? guildId = null, string? voiceChannelId = null)
    {
        var userId = GetUserId();
        var userName = GetUserName();

        var session = _sessionManager.CreateSession(campaignId, userId, userName);

        // Rejoindre le groupe SignalR
        await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId);

        _sessionManager.JoinSession(session.SessionId, userId, userName, Context.ConnectionId);

        _logger.LogInformation("Session {SessionId} created by {UserId}", session.SessionId, userId);

        await Clients.Group(GetCampaignGroup(campaignId)).SendAsync("SessionStarted", new
        {
            sessionId = session.SessionId,
            campaignId = campaignId,
            startedByUserId = userId,
            startedByUserName = userName,
            timestamp = DateTime.UtcNow
        });

        if (!string.IsNullOrWhiteSpace(guildId) && !string.IsNullOrWhiteSpace(voiceChannelId))
        {
            await Clients.Group(GetActivityGroup(guildId, voiceChannelId))
                .SendAsync("ActivitySessionStarted", new
                {
                    sessionId = session.SessionId,
                    campaignId = campaignId,
                    startedByUserId = userId,
                    startedByUserName = userName,
                    guildId,
                    voiceChannelId,
                    timestamp = DateTime.UtcNow
                });
        }

        return MapToSessionInfo(session);
    }

    /// <summary>
    /// S'abonner aux notifications d'une campagne (ex: "SessionStarted").
    /// À appeler depuis une page de campagne / lobby.
    /// </summary>
    public async Task SubscribeCampaign(Guid campaignId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetCampaignGroup(campaignId));
        _logger.LogDebug("Connection {ConnectionId} subscribed to campaign {CampaignId}",
            Context.ConnectionId, campaignId);
    }

    /// <summary>
    /// Se désabonner des notifications d'une campagne.
    /// </summary>
    public async Task UnsubscribeCampaign(Guid campaignId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetCampaignGroup(campaignId));
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from campaign {CampaignId}",
            Context.ConnectionId, campaignId);
    }

    /// <summary>
    /// S'abonner aux notifications "activité Discord" (participants connectés au même salon vocal).
    /// </summary>
    public async Task SubscribeActivity(string guildId, string voiceChannelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetActivityGroup(guildId, voiceChannelId));
        _logger.LogDebug("Connection {ConnectionId} subscribed to activity {GuildId}/{VoiceChannelId}",
            Context.ConnectionId, guildId, voiceChannelId);
    }

    /// <summary>
    /// Se désabonner des notifications "activité Discord".
    /// </summary>
    public async Task UnsubscribeActivity(string guildId, string voiceChannelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetActivityGroup(guildId, voiceChannelId));
        _logger.LogDebug("Connection {ConnectionId} unsubscribed from activity {GuildId}/{VoiceChannelId}",
            Context.ConnectionId, guildId, voiceChannelId);
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
            var resolvedSessionId = result.Session.SessionId;

            // Rejoindre le groupe SignalR
            await Groups.AddToGroupAsync(Context.ConnectionId, resolvedSessionId);

            // Notifier les autres joueurs
            await Clients.OthersInGroup(resolvedSessionId).SendAsync("PlayerJoined", new
            {
                userId,
                userName,
                timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("User {UserId} joined session {SessionId} (requested: {Requested})",
                userId, resolvedSessionId, sessionId);
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
    /// Créer une room standalone (sans campagne) pour le mode multijoueur libre.
    /// </summary>
    public async Task<SessionInfo> CreateRoom(int maxPlayers)
    {
        var userId = GetUserId();
        var userName = GetUserName();

        var session = _sessionManager.CreateRoom(userId, userName, maxPlayers);

        await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId);

        // Map the connection so we can find the session later
        _sessionManager.JoinSession(session.SessionId, userId, userName, Context.ConnectionId);

        _logger.LogInformation("Room {SessionId} created by {UserId}", session.SessionId, userId);

        return MapToSessionInfo(session);
    }

    /// <summary>
    /// Sélectionner un personnage pour le lobby (avant le lancement de la partie).
    /// </summary>
    public async Task SelectCharacter(Guid? characterId)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var userId = GetUserId();
        _sessionManager.SetPlayerCharacter(sessionId, userId, characterId);

        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return;

        // Broadcast updated session info to all players
        var sessionInfo = MapToSessionInfo(session);
        await Clients.Group(sessionId).SendAsync("PlayerUpdated", sessionInfo);
    }

    /// <summary>
    /// Lancer la partie (host/DM uniquement). Construit les UnitAssignments à partir des personnages sélectionnés.
    /// </summary>
    public async Task StartGame(string mapId, string? mapData = null)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        if (session.DmUserId != userId)
            throw new HubException("Only the host can start the game");

        if (session.State != SessionState.Lobby)
            throw new HubException("Game already started");

        // Set session state
        session.State = SessionState.InProgress;
        session.MapId = mapId;
        session.LastActivityAt = DateTime.UtcNow;

        // Build unit assignments for each player
        var assignments = new List<UnitAssignment>();
        foreach (var player in session.Players)
        {
            UnitAssignment assignment;
            if (player.SelectedCharacterId.HasValue)
            {
                try
                {
                    var character = await _characterLookup.GetCharacterAsync(player.SelectedCharacterId.Value);
                    if (character != null)
                    {
                        assignment = new UnitAssignment
                        {
                            UserId = player.UserId,
                            UnitId = $"player_{player.UserId.ToString("N")[..8]}",
                            UnitName = character.Name,
                            CharacterClass = character.CharacterClass,
                            MaxHp = character.MaxHitPoints,
                            CurrentHp = character.CurrentHitPoints,
                            ArmorClass = character.ArmorClass,
                            Speed = character.Speed,
                            Initiative = character.Initiative,
                            AttackDamage = 15,
                            Defense = character.ArmorClass,
                            MovementRange = character.Speed / 5,
                            AttackRange = 1
                        };
                    }
                    else
                    {
                        assignment = BuildDefaultAssignment(player);
                    }
                }
                catch
                {
                    assignment = BuildDefaultAssignment(player);
                }
            }
            else
            {
                assignment = BuildDefaultAssignment(player);
            }

            assignments.Add(assignment);
        }

        var payload = new GameStartedPayload
        {
            MapId = mapId,
            MapData = mapData,
            UnitAssignments = assignments
        };

        _logger.LogInformation("Game started in session {SessionId} with map {MapId} and {Count} players",
            sessionId, mapId, assignments.Count);

        await Clients.Group(sessionId).SendAsync("GameStarted", payload);
    }

    private static UnitAssignment BuildDefaultAssignment(SessionPlayer player)
    {
        return new UnitAssignment
        {
            UserId = player.UserId,
            UnitId = $"player_{player.UserId.ToString("N")[..8]}",
            UnitName = player.UserName ?? "Aventurier",
            CharacterClass = "Guerrier",
            MaxHp = 120,
            CurrentHp = 120,
            ArmorClass = 15,
            Speed = 30,
            Initiative = 12,
            AttackDamage = 20,
            Defense = 15,
            MovementRange = 6,
            AttackRange = 1
        };
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

    /// <summary>
    /// Récupère l'ID utilisateur (Guid) depuis le JWT.
    /// Utilise le même service que l'API Campaign pour convertir l'ID Discord en Guid.
    /// </summary>
    private Guid GetUserId()
    {
        return _userContextService.GetCurrentUserId();
    }

    private string GetUserName()
    {
        return Context.User?.FindFirst("username")?.Value
               ?? Context.User?.FindFirst("name")?.Value
               ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
               ?? "Unknown";
    }

    private static string GetCampaignGroup(Guid campaignId) => $"campaign_{campaignId:N}";
    private static string GetActivityGroup(string guildId, string voiceChannelId)
        => $"activity_{guildId}_{voiceChannelId}";

    private SessionInfo MapToSessionInfo(GameSession session)
    {
        return new SessionInfo
        {
            SessionId = session.SessionId,
            JoinCode = session.JoinCode,
            CampaignId = session.CampaignId,
            PlayerCount = session.Players.Count,
            MaxPlayers = session.MaxPlayers,
            State = session.State,
            MapId = session.MapId,
            Players = session.Players.Select(p => new PlayerInfo
            {
                UserId = p.UserId,
                UserName = p.UserName ?? "Inconnu",
                Role = p.Role,
                Status = p.Status,
                SelectedCharacterId = p.SelectedCharacterId
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

        // Ownership: only allow moving your own player unit id (player_{guid8})
        var expectedUnitId = $"player_{userId.ToString("N")[..8]}";
        if (!string.Equals(request.UnitId, expectedUnitId, StringComparison.OrdinalIgnoreCase))
        {
            return new MoveResult
            {
                UnitId = request.UnitId,
                Success = false,
                Error = "You cannot move a unit you do not control."
            };
        }

        // Minimal collision validation (Free Roam first): disallow moving onto an already-occupied tile
        var target = new GridPosition(request.TargetX, request.TargetY);

        // Fast lookup of existing positions for this session
        var sessionPositions = _positions.GetSessionPositions(sessionId);
        GridPosition? currentPos = null;
        if (sessionPositions.TryGetValue(request.UnitId, out var existing))
        {
            currentPos = existing;
        }

        foreach (var kv in sessionPositions)
        {
            var otherUnitId = kv.Key;
            if (string.Equals(otherUnitId, request.UnitId, StringComparison.OrdinalIgnoreCase)) continue;
            var otherPos = kv.Value;
            if (otherPos.X == target.X && otherPos.Y == target.Y)
            {
                return new MoveResult
                {
                    UnitId = request.UnitId,
                    Success = false,
                    Error = "Target tile is occupied."
                };
            }
        }

        // Ensure path is always provided to clients (client handler ignores empty path)
        var path = request.Path != null && request.Path.Count > 0
            ? request.Path
            : (currentPos != null
                ? new List<GridPosition> { currentPos, target }
                : new List<GridPosition> { target });

        // Commit server-side position
        _positions.SetPosition(sessionId, request.UnitId, target);

        var result = new MoveResult
        {
            UnitId = request.UnitId,
            Path = path,
            ApCost = Math.Max(1, path.Count - 1),
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

        await TryRunEnemyTurnsFromSnapshot(sessionId);
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

        await Clients.OthersInGroup(sessionId).SendAsync("UnitMoved", message);
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

        await TryRunEnemyTurnsFromSnapshot(sessionId);
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

    private async Task TryRunEnemyTurnsFromSnapshot(string sessionId)
    {
        var snapshot = _stateManager.GetSnapshot(sessionId);
        var combat = snapshot?.CombatState;
        if (snapshot == null || combat == null) return;
        if (!combat.IsActive) return;
        if (combat.InitiativeOrder == null || combat.InitiativeOrder.Count == 0) return;

        // Safety: avoid infinite loops
        for (var guard = 0; guard < 10; guard++)
        {
            var currentUnitId = combat.CurrentUnitId;
            if (string.IsNullOrWhiteSpace(currentUnitId)) return;
            if (!currentUnitId.StartsWith("enemy_", StringComparison.OrdinalIgnoreCase)) return;

            var enemy = snapshot.Units.FirstOrDefault(u => string.Equals(u.UnitId, currentUnitId, StringComparison.OrdinalIgnoreCase));
            if (enemy == null) return;

            // Find nearest player
            var players = snapshot.Units.Where(u => u.UnitId.StartsWith("player_", StringComparison.OrdinalIgnoreCase)).ToList();
            if (players.Count == 0) return;
            UnitState nearest = players[0];
            var bestDist = int.MaxValue;
            foreach (var p in players)
            {
                var d = Math.Abs(p.Position.X - enemy.Position.X) + Math.Abs(p.Position.Y - enemy.Position.Y);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = p;
                }
            }

            // If adjacent, do a simple attack (fixed damage)
            if (bestDist <= 1)
            {
                var damage = 8;
                nearest.Hp = Math.Max(0, nearest.Hp - damage);

                var attackResult = new AttackResult
                {
                    AttackerId = enemy.UnitId,
                    TargetId = nearest.UnitId,
                    AbilityId = "enemy_basic_attack",
                    DiceRoll = 0,
                    Modifier = 0,
                    Hit = true,
                    Damage = damage,
                    Effects = null,
                    Success = true
                };
                var attackMsg = _messageSequencer.CreateMessage(sessionId, "AttackResolved", attackResult);
                await Clients.Group(sessionId).SendAsync("AttackResolved", attackMsg);
            }
            else
            {
                // Move 1 step toward the nearest player (no map validation in MVP snapshot-AI)
                var dx = Math.Sign(nearest.Position.X - enemy.Position.X);
                var dy = Math.Sign(nearest.Position.Y - enemy.Position.Y);

                // Prefer X movement then Y
                var target = new GridPosition(enemy.Position.X + dx, enemy.Position.Y);
                if (dx == 0) target = new GridPosition(enemy.Position.X, enemy.Position.Y + dy);

                // Avoid moving onto an occupied tile (based on snapshot)
                var occupied = snapshot.Units.Any(u => !string.Equals(u.UnitId, enemy.UnitId, StringComparison.OrdinalIgnoreCase)
                                                      && u.Position.X == target.X && u.Position.Y == target.Y
                                                      && u.Hp > 0);
                if (!occupied)
                {
                    var start = enemy.Position;
                    enemy.Position = target;
                    _positions.SetPosition(sessionId, enemy.UnitId, target);

                    var moveResult = new MoveResult
                    {
                        UnitId = enemy.UnitId,
                        Path = new List<GridPosition> { start, target },
                        ApCost = 1,
                        Success = true
                    };
                    var moveMsg = _messageSequencer.CreateMessage(sessionId, "UnitMoved", moveResult);
                    await Clients.Group(sessionId).SendAsync("UnitMoved", moveMsg);
                }
            }

            // Advance to next unit in initiative order
            var idx = combat.InitiativeOrder.FindIndex(e => string.Equals(e.UnitId, currentUnitId, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return;
            var nextIdx = (idx + 1) % combat.InitiativeOrder.Count;
            combat.CurrentUnitId = combat.InitiativeOrder[nextIdx].UnitId;

            // Persist updated snapshot so clients can RequestFullState
            snapshot.LastSequenceNumber = Math.Max(snapshot.LastSequenceNumber, 0);
            _stateManager.SetSnapshot(sessionId, snapshot);

            // Broadcast enemy turn ended (optional for client UI)
            var endPayload = new TurnEndedPayload { UnitId = currentUnitId };
            var endMsg = _messageSequencer.CreateMessage(sessionId, "TurnEnded", endPayload);
            await Clients.Group(sessionId).SendAsync("TurnEnded", endMsg);
        }
    }

    #endregion
}