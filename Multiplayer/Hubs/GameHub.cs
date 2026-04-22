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
    private readonly IUserContextService _userContextService;
    private readonly ICharacterLookupService _characterLookup;
    private readonly IInventoryGrantService _inventoryGrant;
    private readonly ICampaignMapLookupService _mapLookup;
    private readonly CombatManager _combatManager;

    /// <summary>
    /// Constructeur du GameHub
    /// </summary>
    public GameHub(ILogger<GameHub> logger, SessionManager sessionManager, MessageSequencer messageSequencer,
        StateManager stateManager, IGameActionValidator validator, IUserContextService userContextService,
        ICharacterLookupService characterLookup, IInventoryGrantService inventoryGrant,
        ICampaignMapLookupService mapLookup, CombatManager combatManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _messageSequencer = messageSequencer;
        _stateManager = stateManager;
        _validator = validator;
        _userContextService = userContextService;
        _characterLookup = characterLookup;
        _inventoryGrant = inventoryGrant;
        _mapLookup = mapLookup;
        _combatManager = combatManager;
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
        var assignments = session.Combat.Units.Values
            .Where(u => u.Team == UnitTeam.Player || u.Team == UnitTeam.Ally)
            .Select(u => new UnitAssignment
            {
                UserId = u.OwnerUserId ?? Guid.Empty,
                UnitId = u.UnitId,
                UnitName = u.Name,
                MaxHp = u.MaxHp,
                CurrentHp = u.CurrentHp,
                Initiative = u.Initiative,
                MovementRange = 6,
                AttackRange = 1,
            })
            .ToList();

        // Resolve the map blob so the reconnecting client can actually render
        // the board. Without this the front sits on "Setting up…" forever
        // because GameStarted arrives with MapData = null.
        string? mapData = null;
        if (session.CampaignId is Guid campaignIdForMap
            && !string.IsNullOrEmpty(session.MapId)
            && Guid.TryParse(session.MapId, out var mapGuid))
        {
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

        await Clients.Caller.SendAsync("GameStarted", new GameStartedPayload
        {
            MapId = session.MapId ?? string.Empty,
            MapData = mapData,
            UnitAssignments = assignments,
        });

        // If combat is active, replay CombatStarted so the caller picks up the
        // turn order + current unit without re-rolling initiative. Resolved /
        // FreeRoam phases are skipped — GameStarted above is enough.
        if (session.Combat.Phase != CombatPhase.FreeRoam
            && session.Combat.Phase != CombatPhase.Resolved
            && session.Combat.TurnOrder.Count > 0)
        {
            var combatPayload = new CombatStartedPayload
            {
                Phase = session.Combat.Phase,
                Round = session.Combat.Round,
                CurrentUnitId = session.Combat.CurrentUnitId,
                TurnOrder = session.Combat.TurnOrder.ToList(),
                Units = session.Combat.Units.Values.ToList(),
                InitiativeOrder = session.Combat.TurnOrder
                    .Select(id => new InitiativeEntry
                    {
                        UnitId = id,
                        Initiative = session.Combat.Units.TryGetValue(id, out var u) ? u.Initiative : 0,
                        ControllerId = session.Combat.Units.TryGetValue(id, out var u2) ? (u2.OwnerUserId ?? Guid.Empty) : Guid.Empty,
                    })
                    .ToList(),
            };
            var message = _messageSequencer.CreateMessage(session.SessionId, "CombatStarted", combatPayload);
            await Clients.Caller.SendAsync("CombatStarted", message);
        }

        _logger.LogInformation("Rejoined user {UserId} to session {SessionId} (phase {Phase})",
            userId, session.SessionId, session.Combat.Phase);
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

        // Set session state
        session.State = SessionState.InProgress;
        session.MapId = mapId;
        session.LastActivityAt = DateTime.UtcNow;

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
                    // Log the failure so a silent "everyone gets defaults" regression
                    // is visible in telemetry, but still fall back to a default
                    // assignment so the game can start.
                    _logger.LogWarning(ex,
                        "Character lookup failed for player {UserId} (characterId {CharacterId}); falling back to default assignment",
                        player.UserId, player.SelectedCharacterId);
                    assignment = BuildDefaultAssignment(player, player.SelectedDefaultTemplate);
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
                    CurrentHp = a.CurrentHp,
                    MaxHp = a.MaxHp,
                    CurrentAp = 4,
                    MaxAp = 4,
                    Initiative = a.Initiative,
                };
            }
        }
        finally
        {
            session.Combat.Lock.Release();
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
                SelectedDefaultTemplate = p.SelectedDefaultTemplate,
            }).ToList()
        };
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

        var result = await _combatManager.StartCombatAsync(session, session.Combat.Units.Values.ToList());

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
            throw new HubException("Only the DM can force-move tokens");

        _logger.LogInformation("DM {UserId} force-moved unit {UnitId} to ({X},{Y}) in session {SessionId}",
            userId, payload.UnitId, payload.Target.X, payload.Target.Y, sessionId);

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

        var rng = new Random();
        var result = rng.Next(1, diceType + 1);

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
        int maxHp = 10, currentHp = 10, maxAp = 4, currentAp = 4, initiative = 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload.StatsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("maxHealth", out var mh) && mh.TryGetInt32(out var mhVal)) maxHp = mhVal;
            if (root.TryGetProperty("currentHealth", out var ch) && ch.TryGetInt32(out var chVal)) currentHp = chVal;
            if (root.TryGetProperty("maxActionPoints", out var map) && map.TryGetInt32(out var mapVal)) maxAp = mapVal;
            if (root.TryGetProperty("currentActionPoints", out var cap) && cap.TryGetInt32(out var capVal)) currentAp = capVal;
            if (root.TryGetProperty("initiative", out var ini) && ini.TryGetInt32(out var iniVal)) initiative = iniVal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DmSpawnUnit: failed to parse statsJson for unit {UnitId}; using defaults", payload.UnitId);
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

    #endregion

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

        var advance = await _combatManager.EndTurnAsync(session, payload.UnitId);
        if (advance == null)
        {
            _logger.LogWarning("EndTurn rejected by CombatManager for unit {UnitId} in session {SessionId}",
                payload.UnitId, sessionId);
            return;
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
    /// Envoyer l'utilisation d'une capacité
    /// <paramref name="payload"/>
    /// </summary>
    public async Task SendAbilityUsed(AbilityUsedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        var userId = GetUserId();
        if (!CanControlUnit(session, payload.UnitId, userId))
            throw new HubException("You do not control that unit");

        if (!IsLegalCombatAction(session, payload.UnitId))
            throw new HubException("Unit cannot act right now");

        // Apply the payload's effects to server-side Combat state so the
        // post-turn snapshot in TurnEnded.Units stays consistent with the
        // damage the clients are showing. Without this, the next TurnEnded
        // broadcast overwrites the attack's HP drop with the stale pre-attack
        // server value.
        await session.Combat.Lock.WaitAsync();
        try
        {
            foreach (var effect in payload.Effects)
            {
                if (!session.Combat.Units.TryGetValue(effect.TargetId, out var target)) continue;
                var kind = effect.Type?.ToLowerInvariant();
                var delta = kind == "heal" ? effect.Value : -effect.Value;
                target.CurrentHp = Math.Clamp(target.CurrentHp + delta, 0, target.MaxHp);
            }
            if (session.Combat.Units.TryGetValue(payload.UnitId, out var attacker))
            {
                if (payload.ApCost > 0)
                {
                    attacker.CurrentAp = Math.Max(0, attacker.CurrentAp - payload.ApCost);
                }
            }
        }
        finally
        {
            session.Combat.Lock.Release();
        }

        var message = _messageSequencer.CreateMessage(sessionId, "AbilityUsed", payload);

        _logger.LogInformation("Ability {AbilityId} used by unit {UnitId} in session {SessionId} — {TargetCount} target(s), {TotalDamage} dmg",
            payload.AbilityId, payload.UnitId, sessionId,
            payload.Effects.Count,
            payload.Effects.Sum(e => e.Value));

        await Clients.Group(sessionId).SendAsync("AbilityUsed", message);
    }

    /// <summary>
    /// Terminer le tour d'une unité
    /// <paramref name="payload"/>
    /// </summary>
    public async Task SendEndTurn(TurnEndedPayload payload)
    {
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        var userId = GetUserId();
        if (!CanControlUnit(session, payload.UnitId, userId))
            throw new HubException("You do not control that unit");

        // Route through CombatManager so the server actually advances the cursor
        // and detects Victory/Defeat. Previously this method was a pure relay —
        // a malicious client could fabricate a TurnEnded event and broadcast it
        // to the group with any NextUnitId / Phase / Round / Outcome they chose.
        var advance = await _combatManager.EndTurnAsync(session, payload.UnitId);
        if (advance == null)
        {
            _logger.LogWarning("SendEndTurn rejected by CombatManager for unit {UnitId} in session {SessionId}",
                payload.UnitId, sessionId);
            return;
        }

        var outgoing = new TurnEndedPayload
        {
            UnitId = payload.UnitId,
            NextUnitId = advance.CurrentUnitId,
            Phase = advance.Phase,
            Round = advance.Round,
            Outcome = advance.Outcome,
            Units = session.Combat.Units.Values.ToList(),
        };

        var message = _messageSequencer.CreateMessage(sessionId, "TurnEnded", outgoing);

        _logger.LogInformation("Turn ended for unit {UnitId} in session {SessionId}",
            payload.UnitId, sessionId);

        await Clients.Group(sessionId).SendAsync("TurnEnded", message);
    }

    /// <summary>
    /// Envoyer un snapshot complet de l'état du jeu (stocké côté serveur pour RequestFullState). E2.3.
    /// </summary>
    public async Task SendGameStateSnapshot(GameStateSnapshot snapshot)
    {
        // DM-only. Previously any session member could push an arbitrary
        // GameStateSnapshot into StateManager which was then served back to
        // late-joiners via RequestFullState — a state-injection vector. Since
        // the rework replaced state-on-reconnect with the server-computed
        // RejoinSession snapshot, this path is unused in production; gated
        // here as defense-in-depth rather than removed outright.
        var sessionId = _sessionManager.GetSessionByConnection(Context.ConnectionId);
        if (sessionId == null) throw new HubException("Not in a session");

        var session = _sessionManager.GetSession(sessionId)
            ?? throw new HubException("Session not found");

        if (session.DmUserId != GetUserId())
            throw new HubException("Only the DM can push state snapshots");

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