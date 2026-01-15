using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IDiscordAuthService _discordAuthService;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    // Simple in-memory user store (replace with database in production)
    private static Dictionary<string, User> Users = new();

    public AuthController(
        IDiscordAuthService discordAuthService,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        IConfiguration configuration)
    {
        _discordAuthService = discordAuthService;
        _tokenService = tokenService;
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
        var clientId = _configuration["Discord:ClientId"];
        var redirectUri = _configuration["Discord:RedirectUri"];
        
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            _logger.LogError("Discord OAuth configuration is missing");
            return StatusCode(500, new { error = "Discord OAuth is not configured" });
        }

        var scope = "identify email guilds";
        var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
        var encodedScope = Uri.EscapeDataString(scope);
        
        var authUrl = $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri={encodedRedirectUri}&response_type=code&scope={encodedScope}";
        
        return Ok(new DiscordAuthUrlResponse { Url = authUrl });
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
            var user = GetOrCreateUser(discordUser);

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

        // Try to get user from in-memory store
        if (Users.TryGetValue(userId, out var user))
        {
            return Ok(user);
        }

        // User not in memory (server may have restarted) - reconstruct from JWT claims
        var username = User.FindFirst("username")?.Value;
        var email = User.FindFirst("email")?.Value;
        var avatar = User.FindFirst("avatar")?.Value;

        if (string.IsNullOrEmpty(username))
            return Unauthorized(new { error = "Invalid token claims" });

        // Reconstruct user from token claims and store in memory
        var reconstructedUser = new User
        {
            Id = userId,
            Username = username,
            Email = email ?? $"{username}@discord.local",
            DiscordId = userId,
            Avatar = avatar,
            CreatedAt = DateTime.UtcNow,
        };

        Users[userId] = reconstructedUser;
        _logger.LogInformation($"Reconstructed user from JWT claims: {username}");

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

        _logger.LogInformation($"[DEV] Generating test token for user: {username} (ID: {userId})");

        // Create or get user in memory store
        if (!Users.TryGetValue(userId, out var user))
        {
            user = new User
            {
                Id = userId,
                Username = username,
                Email = email,
                DiscordId = userId,
                Avatar = null,
                CreatedAt = DateTime.UtcNow,
            };
            Users[user.Id] = user;
            _logger.LogInformation($"Created dev user: {user.Username}");
        }

        // Generate JWT token
        var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email);

        return Ok(new AuthTokenResponse
        {
            Token = token,
            User = user,
        });
    }

    /// <summary>
    /// Helper method to get or create user from Discord data
    /// </summary>
    private User GetOrCreateUser(DiscordUserData discordUser)
    {
        if (Users.TryGetValue(discordUser.Id, out var existingUser))
        {
            // Update existing user
            existingUser.Username = discordUser.Username;
            existingUser.Email = discordUser.Email ?? existingUser.Email;
            existingUser.Avatar = discordUser.Avatar;
            return existingUser;
        }

        // Create new user
        var newUser = new User
        {
            Id = discordUser.Id,
            Username = discordUser.Username,
            Email = discordUser.Email ?? $"{discordUser.Username}@discord.local",
            DiscordId = discordUser.Id,
            Avatar = discordUser.Avatar,
            CreatedAt = DateTime.UtcNow,
        };

        Users[newUser.Id] = newUser;
        _logger.LogInformation($"Created new user: {newUser.Username}");

        return newUser;
    }
}