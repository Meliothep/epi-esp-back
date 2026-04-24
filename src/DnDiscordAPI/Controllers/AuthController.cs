using DnDiscord.Campaign.Common;
using DnDiscord.Campaign.DataAccess;
using DnDiscordAPI.Auth;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Games.Character.Repositories;
using DnDiscordAPI.Games.Database;
using DnDiscordAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DnDiscordAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IDiscordAuthService _discordAuthService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    private readonly IUserStore _userStore;
    private readonly ICharacterRepository _characterRepository;
    private readonly CampaignDbContext _campaignDb;
    private readonly GamesDbContext _gamesDb;

    public AuthController(
        IDiscordAuthService discordAuthService,
        ITokenService tokenService,
        IUserStore userStore,
        ICharacterRepository characterRepository,
        CampaignDbContext campaignDb,
        GamesDbContext gamesDb,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _discordAuthService = discordAuthService;
        _tokenService = tokenService;
        _userStore = userStore;
        _characterRepository = characterRepository;
        _campaignDb = campaignDb;
        _gamesDb = gamesDb;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Get Discord OAuth URL for frontend to redirect user
    /// </summary>
    [AllowAnonymous]
    [HttpGet("discord/url")]
    public ActionResult<DiscordAuthUrlResponse> GetDiscordAuthUrl()
    {
        var authUrl = GetDiscordOAuthUrl();
        if (string.IsNullOrEmpty(authUrl))
            return StatusCode(500, new { error = "Discord OAuth is not configured" });
        return Ok(new DiscordAuthUrlResponse { Url = authUrl });
    }

    /// <summary>
    /// Redirect (302) to Discord OAuth. Use this for popup/login from contexts where
    /// connect-src CSP blocks fetch (e.g. Discord embed). Optional state = return URL when popup is blocked.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("discord/redirect")]
    public IActionResult DiscordRedirect([FromQuery] string? state = null)
    {
        var authUrl = GetDiscordOAuthUrl(state);
        if (string.IsNullOrEmpty(authUrl))
            return StatusCode(500, new { error = "Discord OAuth is not configured" });
        return Redirect(authUrl);
    }

    private string? GetDiscordOAuthUrl(string? state = null)
    {
        var clientId = _configuration["Discord:ClientId"];
        var redirectUri = _configuration["Discord:RedirectUri"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Discord OAuth configuration is missing");
            return null;
        }
        var scope = "identify email guilds";
        var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
        var encodedScope = Uri.EscapeDataString(scope);
        var url = $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={encodedRedirectUri}&response_type=code&scope={encodedScope}";
        if (!string.IsNullOrEmpty(state))
            url += "&state=" + Uri.EscapeDataString(state);
        return url;
    }

    /// <summary>
    /// OAuth2 callback endpoint for Discord authentication
    /// Exchanges authorization code for access token and creates/updates user
    /// </summary>
    [AllowAnonymous]
    [HttpPost("discord/callback")]
    public async Task<ActionResult<AuthTokenResponse>> DiscordCallback([FromBody] DiscordOAuthRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Code))
            {
                _logger.LogError("Authorization code is missing");
                return BadRequest(new { error = "Authorization code is required" });
            }

            _logger.LogInformation($"[OAUTH] Received Discord authorization code: {request.Code.Substring(0, 10)}...");

            // Exchange code for Discord access token
            var discordAccessToken = await _discordAuthService.ExchangeCodeForTokenAsync(request.Code);

            if (string.IsNullOrEmpty(discordAccessToken))
            {
                _logger.LogError("[OAUTH] Failed to exchange authorization code for Discord access token");
                return BadRequest(new { error = "Failed to authenticate with Discord" });
            }

            _logger.LogInformation($"[OAUTH] Successfully exchanged code for Discord access token");

            // Get user data from Discord
            var discordUser = await _discordAuthService.GetUserDataAsync(discordAccessToken);

            if (discordUser == null)
            {
                _logger.LogError("[OAUTH] Failed to fetch user data from Discord");
                return BadRequest(new { error = "Failed to fetch Discord user data" });
            }

            _logger.LogInformation($"[OAUTH] Successfully fetched Discord user: {discordUser.Username} ({discordUser.Id})");

            // Révocation opportuniste du token d'accès Discord : on ne le
            // persiste pas côté serveur, il n'a plus d'utilité après le
            // fetch user data. Fire-and-forget (best-effort) pour ne pas
            // bloquer la réponse du callback.
            _ = _discordAuthService.RevokeAccessTokenAsync(discordAccessToken);

            // Create or update user in our system
            var user = _userStore.GetOrCreateUser(discordUser);

            // Generate JWT token for frontend (including avatar for session restoration)
            var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email, user.Avatar);

            _logger.LogInformation($"[OAUTH] Generated JWT token for user: {user.Username}");

            return Ok(new AuthTokenResponse
            {
                Token = token,
                User = user,
            });
        }
        catch (AccountTombstonedException ex)
        {
            // L'utilisateur a supprimé son compte (RGPD art. 17) puis relance
            // un flow Discord (souvent silencieusement via l'Activity). On ne
            // peut pas le recréer sans violer l'effacement — mais renvoyer
            // 500 laisse le front dans le noir. 410 Gone + code d'erreur
            // structuré permet d'afficher un message actionnable : révoquer
            // l'accès côté Discord avant de se réinscrire (reviewer N2).
            _logger.LogWarning(
                "[OAUTH] Rejected re-authentication for tombstoned Discord account {DiscordId}",
                ex.DiscordId);
            return StatusCode(StatusCodes.Status410Gone, new
            {
                error = "account_previously_deleted",
                message = "Ce compte Discord a été supprimé au titre du droit à l'effacement. " +
                          "Pour vous réinscrire, révoquez d'abord l'accès de DnDiscord dans les " +
                          "paramètres Discord (Applications autorisées), puis reconnectez-vous.",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"[OAUTH] Error in Discord callback: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, new { error = "An error occurred during authentication", details = ex.Message });
        }
    }

    /// <summary>
    /// Get current authenticated user info
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<User> GetCurrentUser()
    {
        // Debug logging for authorization header
        var authHeader = Request.Headers["Authorization"].ToString();
        _logger.LogInformation("[AUTH_ME] Request received. Auth header present: {HasHeader}, Length: {Length}",
            !string.IsNullOrEmpty(authHeader), authHeader?.Length ?? 0);

        var userId = User.FindFirst("sub")?.Value;
        _logger.LogInformation("[AUTH_ME] User claims - UserId: {UserId}", userId ?? "null");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Invalid token" });

        // Bloquer les JWT appartenant à un compte supprimé (RGPD art. 17).
        // Sans ce garde, un token encore valide (TTL 7 jours) permettrait
        // de recréer silencieusement le compte via reconstruction depuis
        // les claims ci-dessous.
        if (_userStore.IsDeleted(userId))
        {
            _logger.LogWarning("[AUTH_ME] Rejected token for deleted account {UserId}", userId);
            return Unauthorized(new { error = "account_deleted" });
        }

        // Try to get user from store (reconstruct if not found after restart)
        var user = _userStore.TryGetUser(userId);
        if (user != null)
        {
            return Ok(user);
        }

        // User not in memory (server may have restarted) - reconstruct from JWT claims
        var username = User.FindFirst("username")?.Value;
        var email = User.FindFirst("email")?.Value;
        var avatar = User.FindFirst("avatar")?.Value;

        if (string.IsNullOrEmpty(username))
            return Unauthorized(new { error = "Invalid token claims" });

        // Reconstruct user from token claims
        var reconstructedUser = new User
        {
            Id = userId,
            Username = username,
            Email = email ?? $"{username}@discord.local",
            DiscordId = userId,
            Avatar = avatar,
            CreatedAt = DateTime.UtcNow,
        };

        _logger.LogInformation("Reconstructed user from JWT claims: {Username}", username);
        return Ok(reconstructedUser);
    }

    /// <summary>
    /// Simple logout endpoint (frontend just removes token from localStorage)
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // In a real app, you might want to blacklist the token or update a session
        _logger.LogInformation("User logged out");
        return Ok();
    }

    /// <summary>
    /// Supprime définitivement le compte utilisateur et l'ensemble de ses
    /// données personnelles (droit à l'effacement — RGPD art. 17 et
    /// Discord Developer Policy). Cascade :
    ///  - Characters (clé : DiscordUserId string)
    ///  - Campagnes possédées (DungeonMasterId) + leurs Snapshots, Members,
    ///    GameSessions, SessionHistoryEntries (cascade EF déjà configurée)
    ///  - Memberships en tant que joueur dans les campagnes d'autrui
    ///  - Entrée in-memory dans UserStore
    ///
    /// La révocation de l'autorisation OAuth côté Discord n'est pas effectuée
    /// côté serveur (nous ne stockons pas le token d'accès Discord). La
    /// politique de confidentialité invite l'utilisateur à révoquer l'accès
    /// depuis « Paramètres Discord → Applications autorisées ».
    /// </summary>
    [Authorize]
    [HttpDelete("me")]
    [EnableRateLimiting("rgpd-destructive")]
    public async Task<IActionResult> DeleteCurrentUser()
    {
        var discordId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(discordId))
            return Unauthorized(new { error = "Invalid token" });

        var userGuid = DiscordIdMapping.ToGuid(discordId);

        _logger.LogInformation(
            "[RGPD_DELETE] Starting account deletion for Discord user {DiscordId} (guid {Guid})",
            discordId, userGuid);

        // 1. Tombstone EN PREMIER : même si la suite échoue, les JWT encore
        //    valides (TTL 7j) seront rejetés par GetCurrentUser. On évite
        //    qu'un utilisateur reste "half-deleted" avec un token fonctionnel.
        _userStore.RemoveUser(discordId);

        int deletedCharacters = 0;
        int deletedOwnedCampaigns = 0;
        int deletedOrphanSessions = 0;
        int deletedMemberships = 0;

        // 2. Games DbContext (Characters) — transaction atomique wrappée
        //    dans l'execution strategy (requis quand EnableRetryOnFailure).
        try
        {
            var gamesStrategy = _gamesDb.Database.CreateExecutionStrategy();
            await gamesStrategy.ExecuteAsync(async () =>
            {
                await using var gamesTx = await _gamesDb.Database.BeginTransactionAsync();
                deletedCharacters = await _characterRepository.DeleteByUserIdAsync(discordId);
                await gamesTx.CommitAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "[RGPD_DELETE] Failed deleting Characters for {DiscordId}. Tombstone set, DB partial.",
                discordId);
            return StatusCode(500, new
            {
                error = "account_deletion_partial",
                stage = "characters",
                tombstoned = true,
            });
        }

        // 3. Campaign DbContext — même pattern. Trois ExecuteDelete dans
        //    une seule transaction : campagnes possédées (cascade EF vers
        //    Snapshots / Members / GameSessions / SessionHistoryEntries),
        //    sessions orphelines StartedBy dans les campagnes d'autrui,
        //    memberships de l'utilisateur dans les campagnes d'autrui.
        //
        //    ⚠️ Note future-toi : ExecuteDeleteAsync court-circuite les
        //    intercepteurs EF et les hooks SaveChanges. Si plus tard on
        //    ajoute un audit interceptor ou des domain events sur la
        //    suppression de Campaign/Character (ex. notifier les
        //    participants en SignalR), il faudra soit fanout manuel AVANT
        //    cet ExecuteDelete, soit basculer sur un load-then-Remove.
        try
        {
            var campaignStrategy = _campaignDb.Database.CreateExecutionStrategy();
            await campaignStrategy.ExecuteAsync(async () =>
            {
                await using var campaignTx = await _campaignDb.Database.BeginTransactionAsync();

                deletedOwnedCampaigns = await _campaignDb.Campaigns
                    .IgnoreQueryFilters()
                    .Where(c => c.DungeonMasterId == userGuid)
                    .ExecuteDeleteAsync();

                // I3 : GameSessions lancées dans les campagnes d'AUTRES DMs
                // (pas cascade-delete par le bloc ci-dessus). History entries
                // partent en cascade EF.
                deletedOrphanSessions = await _campaignDb.GameSessions
                    .Where(s => s.StartedBy == userGuid)
                    .ExecuteDeleteAsync();

                deletedMemberships = await _campaignDb.CampaignMembers
                    .Where(m => m.UserId == userGuid)
                    .ExecuteDeleteAsync();

                await campaignTx.CommitAsync();
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "[RGPD_DELETE] Failed deleting Campaign data for {DiscordId}. characters={Characters} already deleted. Tombstone set.",
                discordId, deletedCharacters);
            return StatusCode(500, new
            {
                error = "account_deletion_partial",
                stage = "campaigns",
                tombstoned = true,
                charactersDeleted = deletedCharacters,
            });
        }

        // Log final de type audit : hash SHA-256 du DiscordId (anonymisé
        // mais vérifiable à la demande), timestamp ISO-8601, counts par
        // type d'entité. Tag [AUDIT_RGPD_DELETE] dédié pour que l'ops
        // puisse configurer une rétention longue (2 ans) sur ce filtre
        // Seq — répond à l'attente "preuve d'effacement" sans nécessiter
        // de nouvelle table dans cette PR.
        var discordIdHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(discordId))).ToLowerInvariant();

        _logger.LogInformation(
            "[AUDIT_RGPD_DELETE] {EventType} at {TimestampUtc:o}, discordIdSha256={DiscordIdHash}, characters={Characters}, ownedCampaigns={OwnedCampaigns}, orphanSessions={OrphanSessions}, memberships={Memberships}",
            "account_deletion_succeeded",
            DateTime.UtcNow,
            discordIdHash,
            deletedCharacters,
            deletedOwnedCampaigns,
            deletedOrphanSessions,
            deletedMemberships);

        return NoContent();
    }

    /// <summary>
    /// Exporte les données personnelles de l'utilisateur au format JSON
    /// (droit à la portabilité — RGPD art. 20). Contenu : profil,
    /// personnages, campagnes possédées, memberships. Le fichier est
    /// renvoyé en attachment pour téléchargement direct.
    /// </summary>
    [Authorize]
    [HttpGet("me/export")]
    [EnableRateLimiting("rgpd-export")]
    public async Task<IActionResult> ExportCurrentUserData()
    {
        var discordId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(discordId))
            return Unauthorized(new { error = "Invalid token" });

        var userGuid = DiscordIdMapping.ToGuid(discordId);

        var user = _userStore.TryGetUser(discordId) ?? new User
        {
            Id = discordId,
            Username = User.FindFirst("username")?.Value ?? string.Empty,
            Email = User.FindFirst("email")?.Value ?? string.Empty,
            DiscordId = discordId,
            Avatar = User.FindFirst("avatar")?.Value,
            CreatedAt = DateTime.UtcNow,
        };

        var characters = await _characterRepository.GetByUserIdAsync(discordId);

        // RGPD art. 20 : n'exporter QUE les données fournies par la personne
        // concernée. On ne doit pas leak les PII des autres joueurs/MJs :
        //  - Snapshots.DataJson contient des données d'autres personnages
        //    (noms, backgrounds, notes) → EXCLU du payload.
        //  - Members d'autres utilisateurs dans les campagnes possédées :
        //    UserId opaqué en hash (structure préservée sans ré-identification),
        //    Nickname/Notes EXCLUS (informations identifiantes sur des tiers,
        //    même si le MJ les a écrites, on ne redistribue pas via l'export).
        var ownedCampaignsRaw = await _campaignDb.Campaigns
            .IgnoreQueryFilters()
            .Where(c => c.DungeonMasterId == userGuid)
            .Include(c => c.Members)
            .Include(c => c.GameSessions)
            .AsNoTracking()
            .ToListAsync();

        var ownedCampaigns = ownedCampaignsRaw.Select(c => new
        {
            c.Id,
            c.Name,
            c.Description,
            c.Status,
            c.CreatedAt,
            c.UpdatedAt,
            c.LastPlayedAt,
            c.SettingsJson,
            c.ImageUrl,
            c.MaxPlayers,
            c.IsPublic,
            c.InviteCode,
            c.CampaignTreeDefinition,
            members = c.Members.Select(m => new
            {
                m.Id,
                memberUserIdOpaque = HashForExport(m.UserId),
                m.Role,
                m.Status,
                m.JoinedAt,
                m.AcceptedAt,
                // Nickname (choisi par le joueur) et Notes (écrites par le
                // MJ sur le joueur) sont volontairement EXCLUS du payload :
                // ce sont des informations identifiantes sur des tiers,
                // et l'export RGPD du MJ ne doit pas servir de vecteur
                // d'exfiltration (reviewer I1). Le MJ conserve l'accès à
                // ces infos via l'interface du service.
            }).ToList(),
            gameSessions = c.GameSessions.Select(s => new
            {
                s.Id,
                s.StartedAt,
                s.EndedAt,
                s.Status,
                s.CurrentNodeId,
                // StartedBy peut être un autre joueur → on opaque.
                startedByOpaque = HashForExport(s.StartedBy),
            }).ToList(),
            // Snapshots.DataJson EXCLU volontairement (contient PII de tiers).
            // Seules les métadonnées des snapshots sont exportées.
            snapshots = _campaignDb.CampaignSnapshots
                .Where(s => s.CampaignId == c.Id)
                .AsNoTracking()
                .Select(s => new
                {
                    s.Id,
                    s.Label,
                    s.Description,
                    s.Version,
                    s.Status,
                    s.CreatedAt,
                    createdByOpaque = HashForExport(s.CreatedBy),
                })
                .ToList(),
        }).ToList();

        var memberships = await _campaignDb.CampaignMembers
            .Where(m => m.UserId == userGuid)
            .AsNoTracking()
            .Select(m => new
            {
                m.Id,
                m.CampaignId,
                m.Role,
                m.Status,
                m.JoinedAt,
                m.AcceptedAt,
                m.Nickname,
                m.Notes,
            })
            .ToListAsync();

        var payload = new
        {
            exportedAt = DateTime.UtcNow,
            schemaVersion = 2,
            notice = "Export RGPD (art. 20). Contient uniquement les données fournies par vous. " +
                     "Les contenus Snapshot.DataJson et les identifiants d'autres utilisateurs " +
                     "sont volontairement exclus ou pseudonymisés.",
            profile = user,
            characters,
            ownedCampaigns,
            memberships,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        });

        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        // S4 : filename avec millisecondes + prefix userGuid pour éviter
        // la collision en cas de double clic dans la même seconde.
        var filename = $"dndiscord-export-{userGuid.ToString()[..8]}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json";
        return File(bytes, "application/json", filename);
    }

    /// <summary>
    /// Opacifie un Guid pour l'export : hash SHA-256 tronqué. Préserve
    /// l'intégrité structurelle (joindre entre snapshots / sessions /
    /// members dans l'export) sans ré-identifier le tiers (irréversible).
    /// </summary>
    private static string HashForExport(Guid id)
    {
        if (id == Guid.Empty) return string.Empty;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(id.ToByteArray());
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// DEV ONLY: Generate a test token without Discord OAuth
    /// </summary>
    [AllowAnonymous]
    [HttpPost("dev/login")]
    public ActionResult<AuthTokenResponse> DevLogin([FromBody] DevLoginRequest request)
    {
        // Only allow in development
        var environment = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return NotFound(); // Hide endpoint in production
        }

        // Default values if not provided
        var userId = request.UserId ?? $"dev-user-{Guid.NewGuid()}";
        var username = request.Username ?? "TestUser";
        var email = request.Email ?? $"{username.ToLower()}@test.local";

        _logger.LogInformation("[DEV] Generating test token for user: {Username} (ID: {UserId})", username, userId);

        User user;
        try
        {
            user = _userStore.GetOrCreateUser(new DiscordUserData
            {
                Id = userId,
                Username = username,
                Email = email,
                Avatar = null,
            });
        }
        catch (AccountTombstonedException ex)
        {
            // Cohérent avec DiscordCallback : renvoyer 410 Gone au lieu d'un
            // 500 opaque quand le DiscordId a été tombstoné. Permet aussi aux
            // tests d'intégration d'asserter le contrat HTTP (reviewer N2).
            _logger.LogWarning(
                "[DEV] Rejected dev-login for tombstoned Discord account {DiscordId}",
                ex.DiscordId);
            return StatusCode(StatusCodes.Status410Gone, new
            {
                error = "account_previously_deleted",
            });
        }

        // Generate JWT token
        var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email);

        return Ok(new AuthTokenResponse
        {
            Token = token,
            User = user,
        });
    }

}