using System.Text.Json.Serialization;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscordAPI.Controllers;

/// <summary>
/// Endpoint pour l'échange du code OAuth Discord (Discord Activity / Embedded App SDK).
/// Le front envoie le code obtenu via discordSdk.commands.authorize(), le backend
/// l'échange contre un token Discord, crée/récupère l'utilisateur et renvoie aussi un JWT.
/// </summary>
[ApiController]
[Route("api/discord")]
public class DiscordController : ControllerBase
{
    private readonly IDiscordAuthService _discordAuthService;
    private readonly ITokenService _tokenService;
    private readonly IUserStore _userStore;
    private readonly ILogger<DiscordController> _logger;

    public DiscordController(
        IDiscordAuthService discordAuthService,
        ITokenService tokenService,
        IUserStore userStore,
        ILogger<DiscordController> logger)
    {
        _discordAuthService = discordAuthService;
        _tokenService = tokenService;
        _userStore = userStore;
        _logger = logger;
    }

    /// <summary>
    /// Échange le code d'autorisation Discord (Activity) contre un token Discord et un JWT.
    /// redirect_uri optionnel : si fourni (ex. origine de l'iframe Activity), il est utilisé pour l'échange.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> ExchangeCode([FromBody] DiscordTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.Code))
        {
            _logger.LogWarning("[DISCORD_ACTIVITY] Token exchange called without code");
            return BadRequest(new { error = "Code is required" });
        }

        try
        {
            var discordAccessToken = await _discordAuthService.ExchangeCodeForTokenAsync(
                request.Code,
                request.RedirectUri);

            if (string.IsNullOrEmpty(discordAccessToken))
            {
                _logger.LogError("[DISCORD_ACTIVITY] Failed to exchange code for Discord token");
                return BadRequest(new { error = "Failed to exchange code" });
            }

            var discordUser = await _discordAuthService.GetUserDataAsync(discordAccessToken);
            if (discordUser == null)
            {
                _logger.LogError("[DISCORD_ACTIVITY] Failed to fetch Discord user");
                return BadRequest(new { error = "Failed to fetch user" });
            }

            var user = _userStore.GetOrCreateUser(discordUser);
            var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email, user.Avatar);

            _logger.LogInformation("[DISCORD_ACTIVITY] Authenticated user: {Username}", user.Username);

            return Ok(new
            {
                access_token = discordAccessToken,
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    discordId = user.DiscordId,
                    avatar = user.Avatar,
                    createdAt = user.CreatedAt,
                },
                token,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DISCORD_ACTIVITY] Error exchanging code");
            return StatusCode(500, new { error = "An error occurred during token exchange" });
        }
    }
}

public record DiscordTokenRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("redirect_uri")] string? RedirectUri = null);
