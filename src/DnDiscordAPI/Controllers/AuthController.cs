using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.Services;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Games.Character.Repositories;
using DnDiscordAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(
        IDiscordAuthService discordAuthService,
        ITokenService tokenService,
        IUserStore userStore,
        ICharacterRepository characterRepository,
        CampaignDbContext campaignDb,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _discordAuthService = discordAuthService;
        _tokenService = tokenService;
        _userStore = userStore;
        _characterRepository = characterRepository;
        _campaignDb = campaignDb;
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
    public async Task<IActionResult> DeleteCurrentUser()
    {
        var discordId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(discordId))
            return Unauthorized(new { error = "Invalid token" });

        var userGuid = UserContextService.ConvertDiscordIdToGuid(discordId);

        _logger.LogInformation(
            "[RGPD_DELETE] Starting account deletion for Discord user {DiscordId} (guid {Guid})",
            discordId, userGuid);

        try
        {
            // Characters (base Games)
            var deletedCharacters = await _characterRepository.DeleteByUserIdAsync(discordId);

            // Campagnes possédées — on ignore le query filter pour attraper
            // aussi les campagnes soft-deleted que l'utilisateur aurait laissées.
            // Cascade EF gère Snapshots / Members / GameSessions / SessionHistoryEntries.
            var deletedOwnedCampaigns = await _campaignDb.Campaigns
                .IgnoreQueryFilters()
                .Where(c => c.DungeonMasterId == userGuid)
                .ExecuteDeleteAsync();

            // Memberships dans les campagnes d'autres DMs
            var deletedMemberships = await _campaignDb.CampaignMembers
                .Where(m => m.UserId == userGuid)
                .ExecuteDeleteAsync();

            // UserStore in-memory
            _userStore.RemoveUser(discordId);

            _logger.LogInformation(
                "[RGPD_DELETE] Account deleted. characters={Characters}, ownedCampaigns={Campaigns}, memberships={Memberships}",
                deletedCharacters, deletedOwnedCampaigns, deletedMemberships);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[RGPD_DELETE] Failed to delete account for Discord user {DiscordId}",
                discordId);
            return StatusCode(500, new { error = "Account deletion failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Exporte les données personnelles de l'utilisateur au format JSON
    /// (droit à la portabilité — RGPD art. 20). Contenu : profil,
    /// personnages, campagnes possédées, memberships. Le fichier est
    /// renvoyé en attachment pour téléchargement direct.
    /// </summary>
    [Authorize]
    [HttpGet("me/export")]
    public async Task<IActionResult> ExportCurrentUserData()
    {
        var discordId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(discordId))
            return Unauthorized(new { error = "Invalid token" });

        var userGuid = UserContextService.ConvertDiscordIdToGuid(discordId);

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

        var ownedCampaigns = await _campaignDb.Campaigns
            .IgnoreQueryFilters()
            .Where(c => c.DungeonMasterId == userGuid)
            .Include(c => c.Members)
            .Include(c => c.Snapshots)
            .Include(c => c.GameSessions)
            .AsNoTracking()
            .ToListAsync();

        var memberships = await _campaignDb.CampaignMembers
            .Where(m => m.UserId == userGuid)
            .AsNoTracking()
            .ToListAsync();

        var payload = new
        {
            exportedAt = DateTime.UtcNow,
            schemaVersion = 1,
            notice = "Export RGPD (art. 20). Données personnelles détenues par DnDiscord pour ce compte.",
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
        var filename = $"dndiscord-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(bytes, "application/json", filename);
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

        var user = _userStore.GetOrCreateUser(new DiscordUserData
        {
            Id = userId,
            Username = username,
            Email = email,
            Avatar = null,
        });

        // Generate JWT token
        var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email);

        return Ok(new AuthTokenResponse
        {
            Token = token,
            User = user,
        });
    }

}