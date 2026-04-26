using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IUserContextService _userContextService;
    private readonly ICharacterLookupService _characterLookup;
    private readonly ICharacterProgressionService _characterProgression;
    private readonly IInventoryGrantService _inventoryGrant;
    private readonly ICampaignMapLookupService _mapLookup;
    private readonly CombatManager _combatManager;
    private readonly SpawnPlacementService _spawnPlacementService;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Constructeur du GameHub
    /// </summary>
    public GameHub(ILogger<GameHub> logger, SessionManager sessionManager, MessageSequencer messageSequencer,
        StateManager stateManager, IUserContextService userContextService,
        ICharacterLookupService characterLookup, ICharacterProgressionService characterProgression,
        IInventoryGrantService inventoryGrant,
        ICampaignMapLookupService mapLookup, CombatManager combatManager,
        SpawnPlacementService spawnPlacementService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _messageSequencer = messageSequencer;
        _stateManager = stateManager;
        _userContextService = userContextService;
        _characterLookup = characterLookup;
        _characterProgression = characterProgression;
        _inventoryGrant = inventoryGrant;
        _mapLookup = mapLookup;
        _combatManager = combatManager;
        _spawnPlacementService = spawnPlacementService;
        _serviceProvider = serviceProvider;
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

        // Auto-rejoin: if this user was already a member of an active session
        // (e.g. they just refreshed the page), re-place them into the SignalR
        // group and push a snapshot so they come back to the same map + combat
        // state without needing to repeat the invite/join flow. Closes BUG-R.
        var existing = _sessionManager.FindSessionByUser(userId);
        if (existing != null)
        {
            try
            {
                await SendRejoinSnapshotAsync(existing, userId, userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Auto-rejoin failed for user {UserId} in session {SessionId}",
                    userId, existing.SessionId);
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Explicit client-initiated rejoin. Safe to call any time — idempotent.
    /// Useful when the automatic rejoin in <see cref="OnConnectedAsync"/> missed
    /// (e.g. race between JWT parse and session lookup) or the user needs a fresh
    /// snapshot.
    /// </summary>
    public async Task<bool> RejoinSession()
    {
        var userId = GetUserId();
        var userName = GetUserName();
        var session = _sessionManager.FindSessionByUser(userId);
        if (session == null) return false;

        await SendRejoinSnapshotAsync(session, userId, userName);
        return true;
    }

    private async Task SendRejoinSnapshotAsync(GameSession session, Guid userId, string userName)
    {
        // Re-associate the connection with the session + group membership.
        _sessionManager.JoinSession(session.SessionId, userId, userName, Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId);

        // Notify peers that the player is back so their "disconnected" banners clear.
        await Clients.OthersInGroup(session.SessionId).SendAsync("PlayerReconnected", new
        {
            userId,
            userName,
            timestamp = DateTime.UtcNow,
        });

        // Send SessionInfo so the caller's own player list is up to date.
        await Clients.Caller.SendAsync("PlayerUpdated", MapToSessionInfo(session));

        // If the game hasn't started yet there's nothing else to replay.
        if (session.State != SessionState.InProgress) return;

        // Replay GameStarted so the caller re-initialises the board with the
        // current map + unit roster. The existing GameStarted handler on the
        // front already routes through startGame() → initializeFreeRoam.
        //
        // Snapshot all combat fields under the lock so concurrent EndTurnAsync /
        // ApplyAbilityAsync cannot produce torn reads (e.g. Phase seen as
        // PlayerTurn while CurrentUnitId already advanced to the next unit).
        List<UnitAssignment> assignments;
        CombatPhase combatPhase;
        int combatRound;
        string? currentUnitId;
        List<string> turnOrderSnapshot;
        List<UnitRuntimeState> unitListSnapshot;

        await session.Combat.Lock.WaitAsync();
        try
        {
            assignments = session.Combat.Units.Values
                .Where(u => u.Team == UnitTeam.Player || u.Team == UnitTeam.Ally)
                .Select(u => new UnitAssignment
                {
                    UserId = u.OwnerUserId ?? Guid.Empty,
                    UnitId = u.UnitId,
                    UnitName = u.Name,
                    // Preserved from the original assignment so the rejoin snapshot
                    // re-seeds the front's UnitType (warrior / mage / archer) via
                    // CharacterToUnit.classToUnitType instead of falling back to
                    // the empty-string default (which spawned warrior for everyone).
                    CharacterClass = u.CharacterClass,
                    MaxHp = u.MaxHp,
                    CurrentHp = u.CurrentHp,
                    Initiative = u.Initiative,
                    MovementRange = 6,
                    AttackRange = 1,
                })
                .ToList();
            combatPhase = session.Combat.Phase;
            combatRound = session.Combat.Round;
            currentUnitId = session.Combat.CurrentUnitId;
            turnOrderSnapshot = session.Combat.TurnOrder.ToList();
            unitListSnapshot = session.Combat.Units.Values.ToList();
        }
        finally
        {
            session.Combat.Lock.Release();
        }

        // Resolve the map blob so the reconnecting client can actually render
        // the board. Without this the front sits on "Setting up…" forever
        // because GameStarted arrives with MapData = null.
        string? mapData = null;
        bool rejoinMapLookupAttempted = false;
        if (session.CampaignId is Guid campaignIdForMap
            && !string.IsNullOrEmpty(session.MapId)
            && Guid.TryParse(session.MapId, out var mapGuid))
        {
            rejoinMapLookupAttempted = true;
            try
            {
                var map = await _mapLookup.GetMapAsync(campaignIdForMap, mapGuid);
                mapData = map?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Map lookup failed during rejoin for session {SessionId}", session.SessionId);
            }
        }

        if (rejoinMapLookupAttempted && string.IsNullOrEmpty(mapData))
        {
            await Clients.Caller.SendAsync("MapLoadFailed", new
            {
                sessionId = session.SessionId,
                mapId = session.MapId,
                reason = "Map data could not be loaded. The board may not render correctly.",
            });
        }

        await Clients.Caller.SendAsync("GameStarted", new GameStartedPayload
        {
            MapId = session.MapId ?? string.Empty,
            MapData = mapData,
            UnitAssignments = assignments,
        });

        // If combat is active, replay CombatStarted so the caller picks up the
        // turn order + current unit without re-rolling initiative. Resolved /
        // FreeRoam phases are skipped — GameStarted above is enough.
        if (combatPhase != CombatPhase.FreeRoam
            && combatPhase != CombatPhase.Resolved
            && turnOrderSnapshot.Count > 0)
        {
            var unitLookup = unitListSnapshot.ToDictionary(u => u.UnitId);
            var combatPayload = new CombatStartedPayload
            {
                Phase = combatPhase,
                Round = combatRound,
                CurrentUnitId = currentUnitId,
                TurnOrder = turnOrderSnapshot,
                Units = unitListSnapshot,
                InitiativeOrder = turnOrderSnapshot
                    .Select(id => new InitiativeEntry
                    {
                        UnitId = id,
                        Initiative = unitLookup.TryGetValue(id, out var u) ? u.Initiative : 0,
                        ControllerId = unitLookup.TryGetValue(id, out var u2) ? (u2.OwnerUserId ?? Guid.Empty) : Guid.Empty,
                    })
                    .ToList(),
            };
            var message = _messageSequencer.CreateMessage(session.SessionId, "CombatStarted", combatPayload);
            await Clients.Caller.SendAsync("CombatStarted", message);
        }

        _logger.LogInformation("Rejoined user {UserId} to session {SessionId} (phase {Phase})",
            userId, session.SessionId, combatPhase);
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
            // Capture the session (and whether the disconnecting user is the DM)
            // BEFORE MarkPlayerDisconnected mutates state.
            var session = _sessionManager.GetSession(sessionId);
            var isDmDisconnecting = session != null && session.DmUserId == userId;

            // Marquer comme déconnecté (grace period pour reconnexion)
            _sessionManager.MarkPlayerDisconnected(Context.ConnectionId);

            // Notifier les autres joueurs
            await Clients.Group(sessionId).SendAsync("PlayerDisconnected", new
            {
                userId = userId,
                connectionId = Context.ConnectionId,
                timestamp = DateTime.UtcNow
            });

            // If the DM just dropped, cancel every in-flight roll request so
            // targeted players stop waiting on results that will never broadcast.
            // Snapshot the keys to keep mutation-during-iteration behaviour
            // explicit; TryRemove guards against races with an explicit
            // DmCancelRollRequest that happens to land in the same instant.
            if (isDmDisconnecting && session != null)
            {
                foreach (var requestId in session.PendingRolls.Keys.ToList())
                {
                    if (session.PendingRolls.TryRemove(requestId, out var pending))
                    {
                        var disconnectCancelPayload = new RollCanceledPayload(requestId, pending.Label, pending.PendingUserIds.ToList());
                        var disconnectCancelMessage = _messageSequencer.CreateMessage(sessionId, "RollCanceled", disconnectCancelPayload);
                        // OthersInGroup excludes the disconnecting DM's still-attached connection
                        // so a grace-period reconnect doesn't see stale "canceled" entries for
                        // requests the server has already removed.
                        await Clients.OthersInGroup(sessionId).SendAsync("RollCanceled", disconnectCancelMessage);
                    }
                }
            }
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
    /// DM quitte la carte en cours et passe au nœud suivant du scénario.
    /// Diffuse <c>CampaignMapExited</c> au groupe campagne pour que les joueurs
    /// naviguent eux aussi vers la page de session et enchaînent le scénario.
    /// </summary>
    /// <param name="campaignId">Identifiant de la campagne.</param>
    /// <param name="nodeId">ID du nœud carte qui vient d'être joué.</param>
    public async Task DmExitMap(Guid campaignId, string nodeId)
    {
        var userId = GetUserId();

        await Clients.Group(GetCampaignGroup(campaignId)).SendAsync("CampaignMapExited", new
        {
            campaignId,
            nodeId,
            exitedByUserId = userId.ToString(),
        });

        _logger.LogDebug("DM {UserId} exited campaign map node {NodeId} in campaign {CampaignId}",
            userId, nodeId, campaignId);
    }

    /// <summary>
    /// DM lance une carte depuis un nœud de type 'map' dans le scénario.
    /// Diffuse <c>CampaignMapLaunched</c> au groupe campagne avec la configuration
    /// complète (mapId, spawnPoint, exitCells, trapCells) pour que les joueurs
    /// puissent aussi naviguer vers le board et charger la même carte.
    /// </summary>
    /// <param name="campaignId">Identifiant de la campagne.</param>
    /// <param name="configJson">Sérialisation JSON de SessionMapConfig côté front.</param>
    public async Task DmLaunchCampaignMap(Guid campaignId, string configJson)
    {
        var userId = GetUserId();

        await Clients.Group(GetCampaignGroup(campaignId)).SendAsync("CampaignMapLaunched", new
        {
            campaignId,
            configJson,
            launchedByUserId = userId.ToString(),
        });

        _logger.LogDebug("DM {UserId} launched campaign map in campaign {CampaignId}",
            userId, campaignId);
    }

    /// <summary>
    /// DM avance le scénario vers le nœud suivant.
    /// Tous les abonnés du groupe campagne reçoivent l'événement "NodeAdvanced"
    /// afin que les joueurs suivent automatiquement sans clic supplémentaire.
    /// </summary>
    /// <param name="campaignId">Identifiant de la campagne.</param>
    /// <param name="fromNodeId">Identifiant du nœud de départ (utilisé comme garde côté client).</param>
    /// <param name="nextNodeId">Identifiant du nœud cible.</param>
    public async Task DmAdvanceNode(Guid campaignId, string fromNodeId, string nextNodeId)
    {
        var userId = GetUserId();

        await Clients.Group(GetCampaignGroup(campaignId)).SendAsync("NodeAdvanced", new
        {
            fromNodeId,
            nextNodeId,
            advancedByUserId = userId.ToString(),
        });

        _logger.LogDebug("DM {UserId} advanced scenario from {FromNode} to {NextNode} in campaign {CampaignId}",
            userId, fromNodeId, nextNodeId, campaignId);
    }

    /// <summary>
    /// Diffuse le vote d'un joueur pour un choix sur un bloc Choices.
    /// Tous les abonnés du groupe campagne reçoivent l'événement "ChoiceVoted".
    /// </summary>
    /// <param name="campaignId">Identifiant de la campagne.</param>
    /// <param name="nodeId">Identifiant du nœud Choices en cours.</param>
    /// <param name="choiceIndex">Index du choix (0-based). -1 = vote annulé.</param>
    public async Task VoteForChoice(Guid campaignId, string nodeId, int choiceIndex)
    {
        var userId   = GetUserId();
        var userName = GetUserName();

        await Clients.Group(GetCampaignGroup(campaignId)).SendAsync("ChoiceVoted", new
        {
            userId   = userId.ToString(),
            userName,
            nodeId,
            choiceIndex,
        });

        _logger.LogDebug("User {UserName} voted choice {ChoiceIndex} on node {NodeId} in campaign {CampaignId}",
            userName, choiceIndex, nodeId, campaignId);
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
    /// Rejoindre la session active d'une campagne.
    /// Cherche dans les sessions in-memory par campaignId — évite le problème
    /// d'utiliser un UUID de base de données (listSessions REST) qui ne correspond
    /// pas à l'ID SignalR in-memory (format "session_GUID").
    /// </summary>
    public async Task<JoinResult> JoinCampaignSession(Guid campaignId)
    {
        var userId   = GetUserId();
        var userName = GetUserName();

        // Find the most-recently-active live session for this campaign.
        var session = _sessionManager
            .GetSessionsByCampaign(campaignId)
            .Where(s => s.State != SessionState.Ended)
            .OrderByDescending(s => s.LastActivityAt)
            .FirstOrDefault();

        if (session == null)
            return JoinResult.Fail("Aucune session active trouvée pour cette campagne.");

        // Delegate to the regular JoinSession logic using the real in-memory sessionId.
        return await JoinSession(session.SessionId);
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
    /// Quitter la session en cours. When the DM leaves, the whole session is
    /// terminated (SessionEnded broadcast + session removed from the manager);
    /// a DM-less session can't progress so keeping it alive just left stranded
    /// players waiting forever. Regular players leave without affecting the
    /// others.
    /// </summary>
    public async Task LeaveSession()
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) return;

        var userId = GetUserId();
        var session = _sessionManager.GetSession(sessionId);
        var isHostLeaving = session?.DmUserId == userId;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        _sessionManager.LeaveSession(Context.ConnectionId);

        if (isHostLeaving)
        {
            await Clients.Group(sessionId).SendAsync("SessionEnded", new
            {
                sessionId,
                reason = "Host left the session",
                timestamp = DateTime.UtcNow,
            });
            _sessionManager.RemoveSession(sessionId);
            _stateManager.ClearSnapshot(sessionId);
            _logger.LogInformation("DM {UserId} ended session {SessionId} by leaving", userId, sessionId);
            return;
        }

        await Clients.Group(sessionId).SendAsync("PlayerLeft", new
        {
            userId,
            reason = "Player left",
            timestamp = DateTime.UtcNow,
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

        // Résoudre le nom du personnage pour l'afficher dans la liste des joueurs
        // sans lookup supplémentaire à chaque broadcast PlayerUpdated.
        string? characterName = null;
        if (characterId.HasValue)
        {
            try
            {
                var character = await _characterLookup.GetCharacterAsync(characterId.Value);
                characterName = character?.Name;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve character name for {CharacterId}", characterId);
            }
        }

        _sessionManager.SetPlayerCharacter(sessionId, userId, characterId, characterName);

        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return;

        // Broadcast updated session info to all players
        var sessionInfo = MapToSessionInfo(session);
        await Clients.Group(sessionId).SendAsync("PlayerUpdated", sessionInfo);
    }

    /// <summary>
    /// Pick a lobby quickstart preset (warrior / mage / archer) instead of a
    /// persisted character. Mutually exclusive with <c>SelectCharacter</c>.
    /// </summary>
    public async Task SelectDefaultTemplate(string? templateId)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        if (templateId is not null && templateId is not ("warrior" or "mage" or "archer"))
            throw new HubException($"Unknown template '{templateId}'");

        var userId = GetUserId();
        _sessionManager.SetPlayerDefaultTemplate(sessionId, userId, templateId);

        var session = _sessionManager.GetSession(sessionId);
        if (session is null) return;

        var sessionInfo = MapToSessionInfo(session);
        await Clients.Group(sessionId).SendAsync("PlayerUpdated", sessionInfo);
    }

    /// <summary>
    /// Lancer la partie (host/DM uniquement). Construit les UnitAssignments à partir des personnages sélectionnés.
    /// </summary>
    public async Task StartGame(string mapId, string? mapData = null)
    {
        await StartOrRestartGameAsync(mapId, mapData, allowRestart: false);
    }

    /// <summary>
    /// DM-only reset of an in-progress session. Rebuilds unit assignments from the
    /// current character selections and re-broadcasts <c>GameStarted</c> to every
    /// client. Plain <c>StartGame</c> refuses when state != Lobby, which left the
    /// only recovery path as "everyone leave and rejoin" — this closes that gap.
    /// </summary>
    public async Task DmRestartGame(string mapId, string? mapData = null)
    {
        await StartOrRestartGameAsync(mapId, mapData, allowRestart: true);
    }

    private async Task StartOrRestartGameAsync(string mapId, string? mapData, bool allowRestart)
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

        if (!allowRestart && session.State != SessionState.Lobby)
            throw new HubException("Game already started");

        // Set session state.
        // KNOWN RACE (POC-scope): State/MapId/LastActivityAt are mutated here
        // without holding session.Combat.Lock. A concurrent EndTurnAsync or
        // DmStartCombat running on the previous combat can observe InProgress
        // while combat.Units still holds the old roster. Acceptable for POC
        // because (a) the DM is the only caller and the client UX serialises
        // DM actions, and (b) widening the lock across the subsequent async
        // map-lookup would require a more complex two-phase commit. Track as
        // tech-debt if concurrent DM tooling is ever added.
        session.State = SessionState.InProgress;
        session.MapId = mapId;
        session.LastActivityAt = DateTime.UtcNow;

        // Resolve mapData server-side when the client passed null. Campaign
        // maps live in Postgres, not localStorage, so the front's DmRestartGame
        // wrapper can't produce mapData for them on its own. Mirror the lookup
        // done in SendRejoinSnapshotAsync so every client gets the blob.
        bool mapLookupAttempted = false;
        if (string.IsNullOrEmpty(mapData)
            && session.CampaignId is Guid campaignIdForMap
            && Guid.TryParse(mapId, out var mapGuid))
        {
            mapLookupAttempted = true;
            try
            {
                var map = await _mapLookup.GetMapAsync(campaignIdForMap, mapGuid);
                mapData = map?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Map lookup failed during (re)start for session {SessionId}", sessionId);
            }
        }

        // If a campaign-map lookup was attempted but returned nothing, every client
        // will be stuck on "Setting up..." — inform the DM so they can retry or
        // switch maps. We still broadcast GameStarted so non-campaign clients
        // (who provide their own mapData) are unaffected.
        if (mapLookupAttempted && string.IsNullOrEmpty(mapData))
        {
            await Clients.Caller.SendAsync("StartFailed", new
            {
                reason = "Map data could not be loaded from the campaign. Try switching maps or reloading.",
                mapId,
            });
        }

        // Build unit assignments for each player. DM is a pure overseer — they don't
        // get a token on the board, even if they happened to select a character earlier.
        var assignments = new List<UnitAssignment>();
        foreach (var player in session.Players.Where(p => p.Role != PlayerRole.DungeonMaster))
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
                catch (Exception ex)
                {
                    // Character lookup can fail for benign reasons (character was
                    // deleted after the player joined the session) or for real
                    // infrastructure reasons (DB timeout, misconfigured service).
                    // Log the failure and notify the affected player so they know
                    // their character was substituted — a misconfigured DB would
                    // otherwise silently downgrade every player to defaults.
                    _logger.LogWarning(ex,
                        "Character lookup failed for player {UserId} (characterId {CharacterId}); falling back to default assignment",
                        player.UserId, player.SelectedCharacterId);
                    assignment = BuildDefaultAssignment(player, player.SelectedDefaultTemplate);

                    var playerConn = session.Players
                        .FirstOrDefault(p => p.UserId == player.UserId)?.ConnectionId;
                    if (!string.IsNullOrEmpty(playerConn))
                    {
                        await Clients.Client(playerConn).SendAsync("CharacterLookupFailed", new
                        {
                            userId = player.UserId,
                            characterId = player.SelectedCharacterId,
                            fallbackTemplate = assignment.CharacterClass,
                        });
                    }
                }
            }
            else
            {
                assignment = BuildDefaultAssignment(player, player.SelectedDefaultTemplate);
            }

            assignments.Add(assignment);
        }

        // Seed server-authoritative combat state with the player roster. Phase stays
        // FreeRoam — initiative is rolled only when DmStartCombat is called.
        // Lock-guarded so a concurrent EndTurnAsync / DmStartCombat can't race
        // with the reset.
        await session.Combat.Lock.WaitAsync();
        try
        {
            session.Combat.Units.Clear();
            session.Combat.Phase = CombatPhase.FreeRoam;
            session.Combat.TurnOrder.Clear();
            session.Combat.CurrentUnitIndex = 0;
            session.Combat.Round = 0;
            session.Combat.Outcome = null;
            foreach (var a in assignments)
            {
                session.Combat.Units[a.UnitId] = new UnitRuntimeState
                {
                    UnitId = a.UnitId,
                    OwnerUserId = a.UserId,
                    Team = UnitTeam.Player,
                    Name = a.UnitName,
                    CharacterClass = a.CharacterClass ?? string.Empty,
                    CurrentHp = a.CurrentHp,
                    MaxHp = a.MaxHp,
                    // Front's CharacterToUnit hardcodes maxActionPoints=6 for
                    // every class. Match it so AP stays in sync across the
                    // TurnEnded.Units snapshot apply. Previously hardcoded 4 on
                    // the server, 6 on the front → AP regenerated to 4/6.
                    CurrentAp = 6,
                    MaxAp = 6,
                    Initiative = a.Initiative,
                };
            }
        }
        finally
        {
            session.Combat.Lock.Release();
        }

        // Compute server-authoritative ally spawn positions so all clients start
        // on the same squares regardless of when their GameStarted arrives.
        // Wrapped in try/catch so a placement bug never leaves the session
        // half-seeded with InProgress set but GameStarted never broadcast.
        if (assignments.Count > 0 && !string.IsNullOrEmpty(mapData))
        {
            try
            {
                var placer = _spawnPlacementService;
                var (walkable, spawnZones, gridWidth, gridHeight) = placer.BuildWalkableGrid(mapData);
                var placementSeed = SpawnPlacementService.StableSeedFromString(sessionId);
                var positions = placer.GetSpawnPositions(
                    walkable, spawnZones, new HashSet<string>(),
                    "ally", assignments.Count, gridWidth, gridHeight, placementSeed);

                for (var i = 0; i < positions.Count && i < assignments.Count; i++)
                {
                    var pos = positions[i];
                    assignments[i].StartX = pos.X;
                    assignments[i].StartY = pos.Y;
                    // Mirror into the live combat roster.
                    if (session.Combat.Units.TryGetValue(assignments[i].UnitId, out var unit))
                    {
                        unit.PositionX = pos.X;
                        unit.PositionY = pos.Y;
                    }
                }

                _logger.LogInformation("Ally placement seeded session {SessionId} with {Count} positions",
                    sessionId, positions.Count);
            }
            catch (Exception ex)
            {
                // Placement failure must not abort the start — clients fall back
                // to their local legacy anchors when StartX/StartY are absent.
                _logger.LogError(ex, "Ally placement failed for session {SessionId}; continuing with absent positions", sessionId);
            }
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

        // Re-broadcast the session info so every client (including a DM that
        // connected after players picked their characters) has fresh
        // selectedCharacterId values — the DmPlayerInspectPanel needs them to
        // look up the player's inventory when the DM clicks their unit.
        var sessionInfo = MapToSessionInfo(session);
        await Clients.Group(sessionId).SendAsync("PlayerUpdated", sessionInfo);
    }

    private static UnitAssignment BuildDefaultAssignment(SessionPlayer player, string? templateId = null)
    {
        // Three preset quickstarts so new players without a persisted character
        // still get class variety. Template ids match the front's button labels.
        var (className, maxHp, ac, speed, init, atk, def, mov, range) = templateId switch
        {
            "mage"   => ("Mage",    80,  12, 30, 14, 22, 12, 6, 6),
            "archer" => ("Archer",  100, 14, 35, 16, 18, 13, 7, 5),
            // "warrior" or null/unknown fall through to the original stats.
            _        => ("Guerrier", 120, 15, 30, 12, 20, 15, 6, 1),
        };

        return new UnitAssignment
        {
            UserId = player.UserId,
            UnitId = $"player_{player.UserId.ToString("N")[..8]}",
            UnitName = player.UserName ?? "Aventurier",
            CharacterClass = className,
            MaxHp = maxHp,
            CurrentHp = maxHp,
            ArmorClass = ac,
            Speed = speed,
            Initiative = init,
            AttackDamage = atk,
            Defense = def,
            MovementRange = mov,
            AttackRange = range,
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
    /// Client-driven replay of pending roll requests. Front invokes this after
    /// DiceRequestListener mounts AND sessionState.hubUserId is populated, so
    /// the events have a real handler + target. Idempotent and safe to invoke
    /// multiple times — front store dedupes by requestId.
    /// Splitting this out of OnConnectedAsync removes a timing race where the
    /// rejoin replay arrived before the front listener was bound, dropping
    /// the events with "No client method with the name 'rollrequested' found".
    /// </summary>
    public async Task RequestRollReplay()
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) return; // not in session, nothing to replay
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) return;
        var userId = GetUserId();

        var isDmUser = session.DmUserId == userId;
        foreach (var pending in session.PendingRolls.Values)
        {
            var isTarget = pending.RollValues.ContainsKey(userId);
            var stillPending = pending.PendingUserIds.Contains(userId);

            if (isDmUser)
            {
                var rejoinDmEchoPayload = new RollRequestedDmEchoPayload(
                    pending.RequestId,
                    pending.DiceType,
                    pending.Label,
                    pending.RollValues.Keys.ToList(),
                    pending.RollValues.Count);
                var rejoinDmEchoMessage = _messageSequencer.CreateMessage(sessionId, "RollRequestedDmEcho", rejoinDmEchoPayload);
                await Clients.Caller.SendAsync("RollRequestedDmEcho", rejoinDmEchoMessage);

                foreach (var (submittedUserId, submittedValue) in pending.SnapshotSubmittedValues())
                {
                    var p = session.Players.FirstOrDefault(pp => pp.UserId == submittedUserId);
                    var rejoinDmResultPayload = new RollResultBroadcastPayload(
                        pending.RequestId,
                        submittedUserId,
                        p?.UserName,
                        pending.DiceType,
                        submittedValue,
                        pending.Label,
                        false); // RequestComplete=false during replay — real completion already occurred
                    var rejoinDmResultMessage = _messageSequencer.CreateMessage(sessionId, "RollResultBroadcast", rejoinDmResultPayload);
                    await Clients.Caller.SendAsync("RollResultBroadcast", rejoinDmResultMessage);
                }
            }
            else if (isTarget && stillPending)
            {
                var rejoinRollRequestedPayload = new RollRequestedPayload(
                    pending.RequestId,
                    pending.DiceType,
                    pending.Label,
                    pending.RollValues[userId]);
                var rejoinRollRequestedMessage = _messageSequencer.CreateMessage(sessionId, "RollRequested", rejoinRollRequestedPayload);
                await Clients.Caller.SendAsync("RollRequested", rejoinRollRequestedMessage);
            }
            else
            {
                var rejoinPublicPayload = new RollRequestedPublicPayload(
                    pending.RequestId,
                    pending.DiceType,
                    pending.Label,
                    pending.RollValues.Keys.ToList(),
                    pending.RollValues.Count);
                var rejoinPublicMessage = _messageSequencer.CreateMessage(sessionId, "RollRequestedPublic", rejoinPublicPayload);
                await Clients.Caller.SendAsync("RollRequestedPublic", rejoinPublicMessage);

                foreach (var (submittedUserId, submittedValue) in pending.SnapshotSubmittedValues())
                {
                    var p = session.Players.FirstOrDefault(pp => pp.UserId == submittedUserId);
                    var rejoinPublicResultPayload = new RollResultBroadcastPayload(
                        pending.RequestId,
                        submittedUserId,
                        p?.UserName,
                        pending.DiceType,
                        submittedValue,
                        pending.Label,
                        false);
                    var rejoinPublicResultMessage = _messageSequencer.CreateMessage(sessionId, "RollResultBroadcast", rejoinPublicResultPayload);
                    await Clients.Caller.SendAsync("RollResultBroadcast", rejoinPublicResultMessage);
                }
            }
        }
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
                SelectedCharacterId = p.SelectedCharacterId,
                SelectedCharacterName = p.SelectedCharacterName,
                SelectedDefaultTemplate = p.SelectedDefaultTemplate,
            }).ToList()
        };
    }

    private static string GetTargetCharacterName(GameSession session, SessionPlayer targetPlayer)
    {
        return session.Combat.Units.Values.FirstOrDefault(unit => unit.OwnerUserId == targetPlayer.UserId)?.Name
            ?? targetPlayer.UserName
            ?? "Inconnu";
    }

    #region [== DM Tools ==]

    /// <summary>
    /// DM switches the session to a different map from the campaign's map pool.
    /// The server looks up the map by id (DM must own the campaign), updates the
    /// session's tracked map id, and broadcasts <c>MapSwitched</c> with the map's
    /// blob to every client so they can reload the scene.
    /// </summary>
    public async Task DmSwitchMap(Guid mapId)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can switch maps");

        if (session.CampaignId is not Guid campaignId)
            throw new HubException("Map switching requires a campaign session");

        var map = await _mapLookup.GetMapAsync(campaignId, mapId)
            ?? throw new HubException("Map not found for this campaign");

        _sessionManager.SetSessionMapId(sessionId, map.Id.ToString());

        _logger.LogInformation("DM {UserId} switched session {SessionId} to map {MapName} ({MapId})",
            session.DmUserId, sessionId, map.Name, map.Id);

        await Clients.Group(sessionId).SendAsync("MapSwitched", new
        {
            mapId = map.Id,
            name = map.Name,
            data = map.Data,
        });
    }

    /// <summary>
    /// DM flips the session from free-roam into combat preparation. All clients
    /// receive <c>CombatStarted</c> and transition their local phase. The actual
    /// <c>COMBAT_PREPARATION → PLAYER_TURN</c> step still happens when players
    /// click the existing "Prêt" button.
    /// </summary>
    public async Task DmStartCombat()
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can start combat");

        // Snapshot the roster under the lock so a concurrent DmSpawnUnit /
        // DmAdjustHp cannot mutate the dictionary while we enumerate it.
        List<UnitRuntimeState> unitSnapshot;
        await session.Combat.Lock.WaitAsync();
        try { unitSnapshot = session.Combat.Units.Values.ToList(); }
        finally { session.Combat.Lock.Release(); }

        var result = await _combatManager.StartCombatAsync(session, unitSnapshot);

        _logger.LogInformation(
            "DM {UserId} started combat in session {SessionId}: {UnitCount} units, first {CurrentUnit}",
            session.DmUserId, sessionId, result.Units.Count, result.CurrentUnitId);

        var payload = new CombatStartedPayload
        {
            Phase = result.Phase,
            Round = result.Round,
            CurrentUnitId = result.CurrentUnitId,
            TurnOrder = result.TurnOrder,
            Units = result.Units,
            // Back-compat fields so legacy front handlers still parse:
            InitiativeOrder = result.TurnOrder
                .Select(id => new InitiativeEntry
                {
                    UnitId = id,
                    Initiative = result.Units.FirstOrDefault(u => u.UnitId == id)?.Initiative ?? 0,
                    ControllerId = result.Units.FirstOrDefault(u => u.UnitId == id)?.OwnerUserId ?? Guid.Empty,
                })
                .ToList(),
        };

        var message = _messageSequencer.CreateMessage(sessionId, "CombatStarted", payload);
        await Clients.Group(sessionId).SendAsync("CombatStarted", message);
    }

    /// <summary>
    /// DM forcibly ends combat, returning the session to free roam on the same map.
    /// Broadcasts <c>CombatEnded</c> so every client clears their turn state together.
    /// Addresses BUG-O.
    /// </summary>
    public async Task DmEndCombat()
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can end combat");

        await _combatManager.EndCombatAsync(session);

        _logger.LogInformation("DM {UserId} ended combat in session {SessionId}", GetUserId(), sessionId);

        var payload = new CombatEndedPayload { Result = CombatResult.Fled, Rewards = new() };
        var message = _messageSequencer.CreateMessage(sessionId, "CombatEnded", payload);
        await Clients.Group(sessionId).SendAsync("CombatEnded", message);
    }

    /// <summary>
    /// DM adjusts a unit's HP (heal or damage, any team). Clamps 0..MaxHp.
    /// Transitions isAlive on zero-cross in either direction. Broadcasts
    /// <c>UnitHpAdjusted</c> so every client applies the same delta + plays
    /// death VFX if the unit just died. When the adjustment wipes out one
    /// side mid-combat, follows up with a <c>TurnEnded</c> broadcast carrying
    /// the outcome so the front's <c>applyTurnEnded</c> flips back to free
    /// roam without waiting on <c>DmEndCombat</c>.
    /// </summary>
    public async Task DmAdjustHp(string unitId, int delta)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");
        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");
        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can adjust HP");
        if (string.IsNullOrWhiteSpace(unitId))
            throw new HubException("unitId required");

        int newHp;
        int maxHp;
        bool wasAlive;
        bool isAlive;
        int appliedDelta;

        await session.Combat.Lock.WaitAsync();
        try
        {
            if (!session.Combat.Units.TryGetValue(unitId, out var unit) || unit is null)
                throw new HubException($"Unit '{unitId}' not in combat roster");

            wasAlive = unit.IsAlive;
            var before = unit.CurrentHp;
            maxHp = unit.MaxHp; // captured inside lock — safe to use after release
            newHp = Math.Clamp(before + delta, 0, maxHp);
            appliedDelta = newHp - before;
            unit.CurrentHp = newHp;
            isAlive = unit.IsAlive; // derived from CurrentHp on UnitRuntimeState
        }
        finally
        {
            session.Combat.Lock.Release();
        }

        _logger.LogInformation("DM {UserId} adjusted {UnitId} HP by {Delta} -> {NewHp}/{MaxHp} (session {SessionId})",
            GetUserId(), unitId, appliedDelta, newHp, maxHp, sessionId);

        var payload = new UnitHpAdjustedPayload
        {
            UnitId = unitId,
            Hp = newHp,
            MaxHp = maxHp,
            IsAlive = isAlive,
            Delta = appliedDelta,
            WasAlive = wasAlive,
        };
        var message = _messageSequencer.CreateMessage(sessionId, "UnitHpAdjusted", payload);
        await Clients.Group(sessionId).SendAsync("UnitHpAdjusted", message);

        // Auto-end combat when the HP mutation wipes out one side. Only fires
        // mid-combat — FreeRoam / already-Resolved sessions return null.
        if (wasAlive && !isAlive)
        {
            var resolution = await _combatManager.DetectOutcomeAsync(session);
            if (resolution != null)
            {
                var turnEnded = new TurnEndedPayload
                {
                    UnitId = unitId,
                    NextUnitId = resolution.CurrentUnitId,
                    Phase = resolution.Phase,
                    Round = resolution.Round,
                    Outcome = resolution.Outcome,
                    Units = session.Combat.Units.Values.ToList(),
                };
                _logger.LogInformation(
                    "Combat auto-resolved by DmAdjustHp in session {SessionId}: {Outcome}",
                    sessionId, resolution.Outcome);
                var turnMessage = _messageSequencer.CreateMessage(sessionId, "TurnEnded", turnEnded);
                await Clients.Group(sessionId).SendAsync("TurnEnded", turnMessage);
            }
        }
    }

    /// <summary>
    /// DM force-moves any token on the board. Broadcasts DmTokenMoved to all players.
    /// </summary>
    public async Task DmMoveToken(DmMoveTokenPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        if (session.DmUserId != userId)
            throw new HubException("Only the DM can move tokens");

        if (payload == null || string.IsNullOrWhiteSpace(payload.UnitId) || payload.Target == null)
            throw new HubException("Invalid payload");

        bool unitFound;
        await session.Combat.Lock.WaitAsync();
        try
        {
            if (session.Combat.Units.TryGetValue(payload.UnitId, out var unit))
            {
                unit.PositionX = payload.Target.X;
                unit.PositionY = payload.Target.Y;
                unitFound = true;
            }
            else
            {
                unitFound = false;
            }
        }
        finally
        {
            session.Combat.Lock.Release();
        }

        if (!unitFound)
        {
            _logger.LogWarning("DmMoveToken: unit {UnitId} not found in session {SessionId} — server state unchanged, broadcast skipped",
                payload.UnitId, sessionId);
            // Notify the DM so their optimistic UI snap-back is explained rather
            // than happening silently on the next TurnEnded broadcast.
            await Clients.Caller.SendAsync("DmMoveTokenRejected", new
            {
                unitId = payload.UnitId,
                reason = "Unit not found in combat roster",
            });
            return;
        }

        var message = _messageSequencer.CreateMessage(sessionId, "DmTokenMoved", payload);
        await Clients.OthersInGroup(sessionId).SendAsync("DmTokenMoved", message);
    }

    /// <summary>
    /// DM rolls a hidden dice visible only to themselves. Not broadcast to players.
    /// </summary>
    public async Task<DmHiddenRollPayload> DmHiddenRoll(int diceType = 20, int modifier = 0, string? label = null)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        if (session.DmUserId != userId)
            throw new HubException("Only the DM can do hidden rolls");

        var result = Random.Shared.Next(1, diceType + 1);

        var payload = new DmHiddenRollPayload
        {
            DiceType = diceType,
            Result = result,
            Modifier = modifier,
            Total = result + modifier,
            Label = label,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("DM {UserId} hidden roll: d{DiceType}={Result}+{Modifier}={Total} in session {SessionId}",
            userId, diceType, result, modifier, result + modifier, sessionId);

        // Only send back to the DM caller — hidden from players
        await Clients.Caller.SendAsync("DmHiddenRollResult", payload);
        return payload;
    }

    /// <summary>
    /// DM grants an item to a player character. Persists via IInventoryService
    /// (which fires InventoryChanged) and broadcasts ItemGranted for the toast.
    /// </summary>
    public async Task DmGrantItem(DmGrantItemPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        if (session.DmUserId != userId)
            throw new HubException("Only the DM can grant items");

        var targetPlayer = session.Players.FirstOrDefault(p => p.UserId == payload.TargetUserId);
        if (targetPlayer == null)
            throw new HubException("Target player not found in session");

        if (targetPlayer.SelectedCharacterId is not Guid characterId)
            throw new HubException("Target player has not selected a character");

        if (session.CampaignId is not Guid campaignId)
            throw new HubException("Session is not attached to a campaign");

        InventoryGrantResult result;
        try
        {
            result = await _inventoryGrant.GrantItemAsync(characterId, payload.ItemId, payload.Quantity, campaignId);
        }
        catch (KeyNotFoundException ex)
        {
            throw new HubException(ex.Message);
        }

        var grantedPayload = new ItemGrantedPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetUserName = targetPlayer.UserName ?? "Inconnu",
            ItemId = payload.ItemId,
            ItemName = result.ItemName,
            Quantity = payload.Quantity,
            Description = payload.Description,
            Timestamp = DateTime.UtcNow,
        };

        _logger.LogInformation(
            "DM {UserId} granted {Quantity}x {ItemName} to character {CharacterId} in session {SessionId}",
            userId, payload.Quantity, result.ItemName, characterId, sessionId);

        var message = _messageSequencer.CreateMessage(sessionId, "ItemGranted", grantedPayload);
        await Clients.Group(sessionId).SendAsync("ItemGranted", message);
    }

    /// <summary>
    /// DM grants raw XP to a player character. XP is converted to one or more
    /// level-ups by the progression service; the resulting stat snapshot is
    /// broadcast through CharacterProgressed.
    /// </summary>
    public async Task DmAwardExperience(DmAwardExperiencePayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can award XP");

        if (payload.TargetUserId == GetUserId())
            throw new HubException("DM cannot self-grant");

        if (payload.ExperienceAmount <= 0)
            throw new HubException("ExperienceAmount must be > 0");

        var targetPlayer = session.Players.FirstOrDefault(p => p.UserId == payload.TargetUserId)
            ?? throw new HubException("Target player not found in session");

        if (targetPlayer.SelectedCharacterId is not Guid characterId)
            throw new HubException("Target player has not selected a character");

        CharacterProgressionResult result;
        try
        {
            result = await _characterProgression.AwardExperienceAsync(characterId, payload.TargetUserId, payload.ExperienceAmount);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "experienceAmount")
        {
            _logger.LogWarning(ex, "DM action validation failed");
            throw new HubException(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "DM action validation failed");
            throw new HubException("Invalid XP award parameters.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "DM XP grant target not found (session {SessionId}, target {TargetUserId})", sessionId, payload.TargetUserId);
            throw new HubException("Target character not found: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "DM XP grant ownership mismatch (session {SessionId}, target {TargetUserId})", sessionId, payload.TargetUserId);
            throw new HubException("Character ownership mismatch: " + ex.Message);
        }

        await SyncCharacterHpToActiveUnitAsync(session, payload.TargetUserId, result.CurrentHitPoints, result.MaxHitPoints);

        var progressed = new CharacterProgressedPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetUserName = targetPlayer.UserName ?? "Inconnu",
            CharacterId = characterId,
            AwardedExperience = result.AwardedExperience,
            ExperienceRemainder = result.ExperienceRemainder,
            PreviousLevel = result.PreviousLevel,
            NewLevel = result.NewLevel,
            LevelUps = result.LevelUps,
            CurrentHitPoints = result.CurrentHitPoints,
            MaxHitPoints = result.MaxHitPoints,
            ArmorClass = result.ArmorClass,
            Initiative = result.Initiative,
            Speed = result.Speed,
            Strength = result.Strength,
            Dexterity = result.Dexterity,
            Constitution = result.Constitution,
            Intelligence = result.Intelligence,
            Wisdom = result.Wisdom,
            Charisma = result.Charisma,
            Timestamp = DateTime.UtcNow,
        };
        var publicProgressed = new CharacterProgressedPublicPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetCharacterName = GetTargetCharacterName(session, targetPlayer),
            NewLevel = result.NewLevel,
            LevelUps = result.LevelUps,
        };

        _logger.LogInformation(
            "DM {UserId} awarded {Xp} XP to {TargetUserId} in session {SessionId} ({Prev}->{New}, +{LevelUps} lvl)",
            GetUserId(), payload.ExperienceAmount, payload.TargetUserId, sessionId,
            result.PreviousLevel, result.NewLevel, result.LevelUps);

        var dmAckMessage = _messageSequencer.CreateMessage(sessionId, "CharacterProgressedDmAck", progressed);
        await Clients.Caller.SendAsync("CharacterProgressedDmAck", dmAckMessage);

        if (!string.IsNullOrEmpty(targetPlayer.ConnectionId))
        {
            var targetMessage = _messageSequencer.CreateMessage(sessionId, "CharacterProgressed", progressed);
            await Clients.Client(targetPlayer.ConnectionId).SendAsync("CharacterProgressed", targetMessage);
        }

        // reduced payload — privacy: hide HP/abilities/XP from non-target
        var excludedConnectionIds = new[] { Context.ConnectionId, targetPlayer.ConnectionId }
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToArray();
        var publicMessage = _messageSequencer.CreateMessage(sessionId, "CharacterProgressedPublic", publicProgressed);
        await Clients.GroupExcept(sessionId, excludedConnectionIds).SendAsync("CharacterProgressedPublic", publicMessage);
    }

    /// <summary>
    /// DM forces one or more level-ups for a player character.
    /// </summary>
    public async Task DmForceLevelUp(DmForceLevelUpPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can level up players");

        if (payload.TargetUserId == GetUserId())
            throw new HubException("DM cannot self-grant");

        if (payload.Levels <= 0)
            throw new HubException("Levels must be > 0");

        var targetPlayer = session.Players.FirstOrDefault(p => p.UserId == payload.TargetUserId)
            ?? throw new HubException("Target player not found in session");

        if (targetPlayer.SelectedCharacterId is not Guid characterId)
            throw new HubException("Target player has not selected a character");

        CharacterProgressionResult result;
        try
        {
            result = await _characterProgression.ForceLevelUpAsync(characterId, payload.TargetUserId, payload.Levels);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "levels")
        {
            _logger.LogWarning(ex, "DM action validation failed");
            throw new HubException(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "DM action validation failed");
            throw new HubException("Invalid level-up parameters.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "DM force-levelup target not found (session {SessionId}, target {TargetUserId})", sessionId, payload.TargetUserId);
            throw new HubException("Target character not found: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "DM force-levelup ownership mismatch (session {SessionId}, target {TargetUserId})", sessionId, payload.TargetUserId);
            throw new HubException("Character ownership mismatch: " + ex.Message);
        }

        await SyncCharacterHpToActiveUnitAsync(session, payload.TargetUserId, result.CurrentHitPoints, result.MaxHitPoints);

        var progressed = new CharacterProgressedPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetUserName = targetPlayer.UserName ?? "Inconnu",
            CharacterId = characterId,
            AwardedExperience = 0,
            ExperienceRemainder = result.ExperienceRemainder,
            PreviousLevel = result.PreviousLevel,
            NewLevel = result.NewLevel,
            LevelUps = result.LevelUps,
            CurrentHitPoints = result.CurrentHitPoints,
            MaxHitPoints = result.MaxHitPoints,
            ArmorClass = result.ArmorClass,
            Initiative = result.Initiative,
            Speed = result.Speed,
            Strength = result.Strength,
            Dexterity = result.Dexterity,
            Constitution = result.Constitution,
            Intelligence = result.Intelligence,
            Wisdom = result.Wisdom,
            Charisma = result.Charisma,
            Timestamp = DateTime.UtcNow,
        };
        var publicProgressed = new CharacterProgressedPublicPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetCharacterName = GetTargetCharacterName(session, targetPlayer),
            NewLevel = result.NewLevel,
            LevelUps = result.LevelUps,
        };

        _logger.LogInformation(
            "DM {UserId} forced {Levels} level-up(s) for {TargetUserId} in session {SessionId} ({Prev}->{New})",
            GetUserId(), payload.Levels, payload.TargetUserId, sessionId,
            result.PreviousLevel, result.NewLevel);

        var dmAckMessage = _messageSequencer.CreateMessage(sessionId, "CharacterProgressedDmAck", progressed);
        await Clients.Caller.SendAsync("CharacterProgressedDmAck", dmAckMessage);

        if (!string.IsNullOrEmpty(targetPlayer.ConnectionId))
        {
            var targetMessage = _messageSequencer.CreateMessage(sessionId, "CharacterProgressed", progressed);
            await Clients.Client(targetPlayer.ConnectionId).SendAsync("CharacterProgressed", targetMessage);
        }

        // reduced payload — privacy: hide HP/abilities/XP from non-target
        var excludedConnectionIds = new[] { Context.ConnectionId, targetPlayer.ConnectionId }
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToArray();
        var publicMessage = _messageSequencer.CreateMessage(sessionId, "CharacterProgressedPublic", publicProgressed);
        await Clients.GroupExcept(sessionId, excludedConnectionIds).SendAsync("CharacterProgressedPublic", publicMessage);
    }

    /// <summary>
    /// DM grants (or removes) gold from a player character wallet.
    /// </summary>
    public async Task DmGrantGold(DmGrantGoldPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId)
            ?? throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can grant gold");

        if (payload.TargetUserId == GetUserId())
            throw new HubException("DM cannot self-grant");

        var amount = payload.Amount;
        if (amount == 0)
            throw new HubException("Amount must not be 0");

        var currencyType = payload.CurrencyType.ToWire();

        var targetPlayer = session.Players.FirstOrDefault(p => p.UserId == payload.TargetUserId)
            ?? throw new HubException("Target player not found in session");

        if (targetPlayer.SelectedCharacterId is not Guid characterId)
            throw new HubException("Target player has not selected a character");

        WalletSnapshotResult wallet;
        try
        {
            wallet = await _characterProgression.AdjustCurrencyAsync(characterId, payload.TargetUserId, currencyType, amount);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "DM gold grant target not found (session {SessionId}, target {TargetUserId})", sessionId, payload.TargetUserId);
            throw new HubException("Target character not found: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "DM gold grant ownership mismatch (session {SessionId}, target {TargetUserId})", sessionId, payload.TargetUserId);
            throw new HubException("Character ownership mismatch: " + ex.Message);
        }

        var goldGranted = new GoldGrantedPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetUserName = targetPlayer.UserName ?? "Inconnu",
            CharacterId = characterId,
            Amount = amount,
            CurrencyType = payload.CurrencyType,
            CopperPieces = wallet.CopperPieces,
            SilverPieces = wallet.SilverPieces,
            ElectrumPieces = wallet.ElectrumPieces,
            GoldPieces = wallet.GoldPieces,
            PlatinumPieces = wallet.PlatinumPieces,
            TotalInCopper = wallet.TotalInCopper,
            Timestamp = DateTime.UtcNow,
        };
        var publicGoldGranted = new GoldGrantedPublicPayload
        {
            TargetUserId = payload.TargetUserId,
            TargetCharacterName = GetTargetCharacterName(session, targetPlayer),
            CurrencyType = payload.CurrencyType,
            Amount = amount,
        };

        _logger.LogInformation(
            "DM {UserId} adjusted currency by {Amount} {Currency} for {TargetUserId} in session {SessionId} (CP {CP}, SP {SP}, EP {EP}, GP {GP}, PP {PP})",
            GetUserId(), amount, currencyType, payload.TargetUserId, sessionId,
            wallet.CopperPieces, wallet.SilverPieces, wallet.ElectrumPieces, wallet.GoldPieces, wallet.PlatinumPieces);

        var dmAckMessage = _messageSequencer.CreateMessage(sessionId, "GoldGrantedDmAck", goldGranted);
        await Clients.Caller.SendAsync("GoldGrantedDmAck", dmAckMessage);

        if (!string.IsNullOrEmpty(targetPlayer.ConnectionId))
        {
            var targetMessage = _messageSequencer.CreateMessage(sessionId, "GoldGranted", goldGranted);
            await Clients.Client(targetPlayer.ConnectionId).SendAsync("GoldGranted", targetMessage);
        }

        var excludedConnectionIds = new[] { Context.ConnectionId, targetPlayer.ConnectionId }
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToArray();
        var publicMessage = _messageSequencer.CreateMessage(sessionId, "GoldGrantedPublic", publicGoldGranted);
        await Clients.GroupExcept(sessionId, excludedConnectionIds).SendAsync("GoldGrantedPublic", publicMessage);
    }

    /// <summary>
    /// DM spawns a new enemy unit on the board. Broadcasts DmUnitSpawned to all players.
    /// </summary>
    public async Task DmSpawnUnit(DmSpawnUnitPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        if (session.DmUserId != userId)
            throw new HubException("Only the DM can spawn units");

        // Parse HP / AP / initiative from the client-provided stats blob so the
        // server's combat state matches what the DM sees on the board. Hard-coding
        // HP=10 meant enemies got one-shot by mid-tier abilities.
        // We throw HubException on parse failure: proceeding with maxHp=10 defaults
        // would spawn a custom-stat boss at 10 HP (one-shot), which is worse than
        // surfacing the error so the DM can fix their payload.
        int maxHp, currentHp, maxAp, currentAp, initiative;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload.StatsJson);
            var root = doc.RootElement;
            maxHp      = root.TryGetProperty("maxHealth",           out var mh)  && mh.TryGetInt32(out var mhVal)  ? mhVal  : 10;
            currentHp  = root.TryGetProperty("currentHealth",       out var ch)  && ch.TryGetInt32(out var chVal)  ? chVal  : maxHp;
            maxAp      = root.TryGetProperty("maxActionPoints",     out var map) && map.TryGetInt32(out var mapVal) ? mapVal : 4;
            currentAp  = root.TryGetProperty("currentActionPoints", out var cap) && cap.TryGetInt32(out var capVal) ? capVal : maxAp;
            initiative = root.TryGetProperty("initiative",          out var ini) && ini.TryGetInt32(out var iniVal) ? iniVal : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DmSpawnUnit: statsJson parse failed for unit {UnitId}", payload.UnitId);
            throw new HubException($"Invalid statsJson for unit '{payload.UnitId}': {ex.Message}");
        }

        var runtime = new UnitRuntimeState
        {
            UnitId = payload.UnitId,
            Team = UnitTeam.Enemy,
            Name = payload.Name,
            PositionX = payload.Target.X,
            PositionY = payload.Target.Y,
            CurrentHp = currentHp,
            MaxHp = maxHp,
            CurrentAp = currentAp,
            MaxAp = maxAp,
            Initiative = initiative,
        };
        await _combatManager.SpawnUnitAsync(session, runtime);

        _logger.LogInformation("DM {UserId} spawned {UnitType} '{Name}' at ({X},{Y}) in session {SessionId}",
            userId, payload.UnitType, payload.Name, payload.Target.X, payload.Target.Y, sessionId);

        // Broadcast to the full group (including the DM) so the new unit's
        // turn-order slot is reflected on every client. Was OthersInGroup — that
        // left the DM's client to guess whether the spawn succeeded.
        var message = _messageSequencer.CreateMessage(sessionId, "DmUnitSpawned", payload);
        await Clients.Group(sessionId).SendAsync("DmUnitSpawned", message);
    }

    private async Task SyncCharacterHpToActiveUnitAsync(
        GameSession session,
        Guid targetUserId,
        int currentHp,
        int maxHp)
    {
        string unitId;
        int snapshotHp;
        int snapshotMaxHp;
        bool isAlive;
        bool wasAlive;
        int oldHp;

        await session.Combat.Lock.WaitAsync();
        try
        {
            var unit = session.Combat.Units.Values
                .FirstOrDefault(u => u.OwnerUserId == targetUserId);

            if (unit == null) return;

            // Capture state before mutation so WasAlive and Delta are correct.
            wasAlive = unit.IsAlive;
            oldHp = unit.CurrentHp;

            unit.MaxHp = maxHp;
            unit.CurrentHp = Math.Clamp(currentHp, 0, maxHp);

            // Snapshot inside the lock to prevent concurrent mutations from
            // producing a stale payload after Lock.Release().
            unitId = unit.UnitId;
            snapshotHp = unit.CurrentHp;
            snapshotMaxHp = unit.MaxHp;
            isAlive = unit.IsAlive;
        }
        finally
        {
            session.Combat.Lock.Release();
        }

        var hpPayload = new UnitHpAdjustedPayload
        {
            UnitId = unitId,
            Hp = snapshotHp,
            MaxHp = snapshotMaxHp,
            IsAlive = isAlive,
            Delta = snapshotHp - oldHp,
            WasAlive = wasAlive,
        };
        var hpMessage = _messageSequencer.CreateMessage(session.SessionId, "UnitHpAdjusted", hpPayload);
        await Clients.Group(session.SessionId).SendAsync("UnitHpAdjusted", hpMessage);
    }

    /// <summary>
    /// DM triggers a d20 roll request for one or more players. Each target
    /// receives a private RollRequested event with their pre-rolled value;
    /// the DM gets a DmEcho; and the whole session sees a public announcement.
    /// </summary>
    public async Task DmRequestRoll(DmRollRequestPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId);
        if (session == null)
            throw new HubException("Session not found");

        var userId = GetUserId();
        if (session.DmUserId != userId)
            throw new HubException("Only the DM can request rolls");

        if (payload.DiceType != "d20")
            throw new HubException("Only d20 is supported in v1");

        if (session.PendingRolls.Count >= 10)
            throw new HubException("Too many open roll requests (max 10)");

        // Require a live ConnectionId alongside the Connected status so the
        // per-target SendAsync below can never silently drop a target that is
        // counted in PendingUserIds — that would leave the request waiting on
        // a player who never received it. If a target reconnects later, their
        // RequestRollReplay invocation rehydrates the pending roll for them.
        var targets = payload.TargetUserIds.Count > 0
            ? payload.TargetUserIds
                .Where(id => id != session.DmUserId && session.Players.Any(p =>
                    p.UserId == id &&
                    p.Role == PlayerRole.Player &&
                    p.Status == ConnectionStatus.Connected &&
                    p.ConnectionId != null))
                .Distinct()
                .ToList()
            : session.Players
                .Where(p => p.UserId != session.DmUserId &&
                            p.Role == PlayerRole.Player &&
                            p.Status == ConnectionStatus.Connected &&
                            p.ConnectionId != null)
                .Select(p => p.UserId)
                .Distinct()
                .ToList();

        if (targets.Count == 0)
            throw new HubException("No valid connected targets in session");

        var requestId = Guid.NewGuid();
        var values = targets.ToDictionary(id => id, _ => Random.Shared.Next(1, 21));
        var pending = new PendingRollRequest
        {
            RequestId = requestId,
            DiceType = "d20",
            Label = payload.Label,
            RollValues = values,
            PendingUserIds = new HashSet<Guid>(targets),
        };

        if (!session.PendingRolls.TryAdd(requestId, pending))
            throw new HubException("Roll request id collision — retry");

        _logger.LogInformation(
            "DmRequestRoll: session={SessionId}, requestId={RequestId}, label={Label}, dice={DiceType}, targetCount={TargetCount}, openPending={OpenPending}",
            sessionId, requestId, payload.Label ?? "(none)", "d20", targets.Count, session.PendingRolls.Count);

        // Per-target send via ConnectionId — DiscordUserIdProvider keys SignalR
        // user routes by Discord snowflake, but session.Players[].UserId is the
        // MD5-derived Guid, so Clients.User(uidGuid) would never match.
        foreach (var (uid, val) in values)
        {
            var targetPlayer = session.Players.FirstOrDefault(p => p.UserId == uid);
            if (targetPlayer?.ConnectionId == null) continue;
            var rollRequestedPayload = new RollRequestedPayload(requestId, "d20", payload.Label, val);
            var rollRequestedMessage = _messageSequencer.CreateMessage(sessionId, "RollRequested", rollRequestedPayload);
            await Clients.Client(targetPlayer.ConnectionId).SendAsync("RollRequested", rollRequestedMessage);
        }

        var dmEchoPayload = new RollRequestedDmEchoPayload(requestId, "d20", payload.Label, targets, targets.Count);
        var dmEchoMessage = _messageSequencer.CreateMessage(sessionId, "RollRequestedDmEcho", dmEchoPayload);
        // DM is the caller of this method — Clients.Caller is the cleanest target
        // and avoids the same UserId Guid vs snowflake mismatch.
        await Clients.Caller.SendAsync("RollRequestedDmEcho", dmEchoMessage);

        var publicPayload = new RollRequestedPublicPayload(requestId, "d20", payload.Label, targets, targets.Count);
        var publicMessage = _messageSequencer.CreateMessage(sessionId, "RollRequestedPublic", publicPayload);
        await Clients.Group(sessionId).SendAsync("RollRequestedPublic", publicMessage);
    }

    public async Task SubmitRollResult(SubmitRollResultPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) throw new HubException("Not in a session");
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) throw new HubException("Session not found");

        var userId = GetUserId();

        if (!session.PendingRolls.TryGetValue(payload.RequestId, out var pending))
        {
            _logger.LogWarning(
                "SubmitRollResult ignored: requestId {RequestId} not found (user {UserId}, session {SessionId})",
                payload.RequestId, userId, sessionId);
            return;
        }

        if (!pending.TrySubmit(userId, out var value, out var complete))
        {
            _logger.LogWarning(
                "SubmitRollResult ignored by TrySubmit: requestId {RequestId}, user {UserId}",
                payload.RequestId, userId);
            return;
        }

        var player = session.Players.FirstOrDefault(p => p.UserId == userId);

        // Persist the roll result to the campaign journal so it survives the
        // in-memory session and shows up in the campaign log. DB failure must
        // not block the live broadcast — the play loop is more important than
        // the journal for POC scope.
        if (session.CampaignId is Guid campaignId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
                db.RollHistory.Add(new RollHistoryEntry
                {
                    CampaignId = campaignId,
                    SessionId = sessionId,
                    RequestId = pending.RequestId,
                    UserId = userId,
                    UserName = player?.UserName,
                    DiceType = pending.DiceType,
                    Value = value,
                    Label = pending.Label,
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist roll result for request {RequestId} session {SessionId}",
                    pending.RequestId, sessionId);
            }
        }

        var rollResultPayload = new RollResultBroadcastPayload(
            pending.RequestId,
            userId,
            player?.UserName,
            pending.DiceType,
            value,
            pending.Label,
            complete);
        var rollResultMessage = _messageSequencer.CreateMessage(sessionId, "RollResultBroadcast", rollResultPayload);
        await Clients.Group(sessionId).SendAsync("RollResultBroadcast", rollResultMessage);

        if (complete)
            session.PendingRolls.TryRemove(pending.RequestId, out _);
    }

    public async Task DmCancelRollRequest(Guid requestId)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) throw new HubException("Not in a session");
        var session = _sessionManager.GetSession(sessionId);
        if (session == null) throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can cancel a roll");

        if (!session.PendingRolls.TryRemove(requestId, out var pending))
            return; // idempotent — already closed

        var cancelPayload = new RollCanceledPayload(requestId, pending.Label, pending.PendingUserIds.ToList());
        var cancelMessage = _messageSequencer.CreateMessage(sessionId, "RollCanceled", cancelPayload);
        await Clients.Group(sessionId).SendAsync("RollCanceled", cancelMessage);
    }

    #endregion

    #region [== Messages de jeu ==]

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
        if (!CanControlUnit(session, payload.UnitId, userId))
            throw new HubException("You do not control that unit");

        var advance = await _combatManager.EndTurnAsync(session, payload.UnitId);
        if (advance == null)
        {
            _logger.LogWarning("EndTurn rejected by CombatManager for unit {UnitId} in session {SessionId}",
                payload.UnitId, sessionId);
            throw new HubException("Turn not allowed: not your turn or wrong combat phase");
        }

        var outgoing = new TurnEndedPayload
        {
            UnitId = payload.UnitId,
            NextUnitId = advance.CurrentUnitId,
            Phase = advance.Phase,
            Round = advance.Round,
            Outcome = advance.Outcome,
            // Include the full unit roster so clients pick up AP reset on round
            // wrap + HP / AP mutations that happened during the outgoing turn.
            Units = session.Combat.Units.Values.ToList(),
        };

        _logger.LogInformation(
            "EndTurn in session {SessionId}: {FromUnit} → {NextUnit} (phase {Phase}, round {Round}, outcome {Outcome})",
            sessionId, payload.UnitId, advance.CurrentUnitId ?? "(none)", advance.Phase, advance.Round, advance.Outcome?.ToString() ?? "-");

        var message = _messageSequencer.CreateMessage(sessionId, "TurnEnded", outgoing);
        await Clients.Group(sessionId).SendAsync("TurnEnded", message);
    }

    /// <summary>
    /// Envoyer un mouvement d'unité (legacy broadcast sans validation serveur).
    /// </summary>
    public async Task SendUnitMove(UnitMovedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        var userId = GetUserId();
        if (!CanControlUnit(session, payload.UnitId, userId))
            throw new HubException("You do not control that unit");

        if (!IsLegalCombatAction(session, payload.UnitId))
            throw new HubException("Unit cannot move right now");

        var message = _messageSequencer.CreateMessage(sessionId, "UnitMoved", payload);

        _logger.LogInformation("Unit {UnitId} moved in session {SessionId}", payload.UnitId, sessionId);

        await Clients.OthersInGroup(sessionId).SendAsync("UnitMoved", message);
    }

    /// <summary>
    /// Authority check — the caller (via their userId) is allowed to issue commands
    /// for the given unit. DM can control any unit; players can only control units
    /// whose <see cref="UnitRuntimeState.OwnerUserId"/> matches their userId.
    /// Returns false when the unit isn't tracked on the server at all.
    /// </summary>
    private static bool CanControlUnit(GameSession session, string unitId, Guid userId)
    {
        if (!session.Combat.Units.TryGetValue(unitId, out var unit)) return false;
        if (session.DmUserId == userId) return true;
        return unit.OwnerUserId == userId;
    }

    /// <summary>
    /// Phase legality — in FreeRoam anything goes. During active combat the unit
    /// must be the current one in the turn order. Resolved combat blocks everything.
    /// </summary>
    private static bool IsLegalCombatAction(GameSession session, string unitId)
    {
        if (session.Combat.Phase == CombatPhase.FreeRoam) return true;
        if (session.Combat.Phase == CombatPhase.Resolved) return false;
        return session.Combat.CurrentUnitId == unitId;
    }

    /// <summary>
    /// Envoyer l'utilisation d'une capacité. Le serveur applique les effets (HP/AP) de façon
    /// autoritaire et diffuse le payload résolu — les peers n'ont plus à dépiler eux-mêmes.
    /// </summary>
    public async Task SendAbilityUsed(AbilityUsedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        var userId = GetUserId();
        if (!CanControlUnit(session, payload.UnitId, userId))
            throw new HubException("You do not control that unit");

        if (!IsLegalCombatAction(session, payload.UnitId))
            throw new HubException("Unit cannot use abilities right now");

        var result = await _combatManager.ApplyAbilityAsync(
            session, payload.UnitId, payload.AbilityId, payload.Effects, payload.ApCost);

        if (result == null)
        {
            _logger.LogWarning("ApplyAbility rejected for unit {UnitId} in session {SessionId}",
                payload.UnitId, sessionId);
            throw new HubException("Ability not allowed: invalid state or insufficient AP");
        }

        var resolved = new AbilityUsedPayload
        {
            UnitId = result.UnitId,
            AbilityId = result.AbilityId,
            Targets = payload.Targets,
            DiceResult = payload.DiceResult,
            Effects = result.Effects.ToList(),
            ApCost = payload.ApCost,
            Cooldown = payload.Cooldown,
        };

        var message = _messageSequencer.CreateMessage(sessionId, "AbilityUsed", resolved);

        _logger.LogInformation("Ability {AbilityId} used by unit {UnitId} in session {SessionId}",
            payload.AbilityId, payload.UnitId, sessionId);

        await Clients.Group(sessionId).SendAsync("AbilityUsed", message);

        // Auto-end combat when a Damage effect kills the last unit on one side.
        var anyKilled = result.Effects.Any(e =>
            e.Type == "Damage"
            && session.Combat.Units.TryGetValue(e.TargetId, out var t)
            && !t.IsAlive);

        if (anyKilled)
        {
            var resolution = await _combatManager.DetectOutcomeAsync(session);
            if (resolution != null)
            {
                var turnEnded = new TurnEndedPayload
                {
                    UnitId = payload.UnitId,
                    NextUnitId = resolution.CurrentUnitId,
                    Phase = resolution.Phase,
                    Round = resolution.Round,
                    Outcome = resolution.Outcome,
                    Units = session.Combat.Units.Values.ToList(),
                };
                _logger.LogInformation(
                    "Combat auto-resolved by ability {AbilityId} in session {SessionId}: {Outcome}",
                    payload.AbilityId, sessionId, resolution.Outcome);
                var turnMessage = _messageSequencer.CreateMessage(sessionId, "TurnEnded", turnEnded);
                await Clients.Group(sessionId).SendAsync("TurnEnded", turnMessage);
            }
        }
    }

    /// <summary>
    /// Envoyer un snapshot complet de l'état du jeu (stocké côté serveur pour RequestFullState). E2.3.
    /// </summary>
    public async Task SendGameStateSnapshot(GameStateSnapshot snapshot)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null)
            throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can push a state snapshot");

        if (snapshot.SessionId != sessionId)
            throw new HubException("Snapshot session id mismatch");

        _stateManager.SetSnapshot(sessionId, snapshot);

        var message = _messageSequencer.CreateMessage(sessionId, "GameStateSnapshot", snapshot);
        _logger.LogDebug("Game state snapshot stored and sent to session {SessionId}", sessionId);
        await Clients.OthersInGroup(sessionId).SendAsync("GameStateSnapshot", message);
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

        var snapshot = _stateManager.GetSnapshot(sessionId);
        if (snapshot == null)
        {
            var session = _sessionManager.GetSession(sessionId);
            if (session != null)
            {
                await session.Combat.Lock.WaitAsync();
                try
                {
                    snapshot = GameStateSnapshot.FromSession(session);
                }
                finally
                {
                    session.Combat.Lock.Release();
                }
                // Only cache when the session exists — if it was just removed the
                // cache entry would be permanent (CleanupStaleSessionsByPolicy
                // already evicted it from _sessions so it will never call ClearSnapshot).
                _stateManager.SetSnapshot(sessionId, snapshot);
            }
            else
            {
                // Session is gone: return an empty snapshot directly without caching.
                snapshot = new GameStateSnapshot { SessionId = sessionId };
            }
        }

        var message = _messageSequencer.CreateMessage(sessionId, "FullStateSync", snapshot);
        await Clients.Caller.SendAsync("FullStateSync", message);
        return message;
    }

    // NOTE: a previous "enemy AI from snapshot" POC lived here.
    // It referenced non-existent snapshot types and was removed to keep the hub buildable.

    #endregion
}