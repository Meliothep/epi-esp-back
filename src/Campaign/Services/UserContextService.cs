using DnDiscord.Campaign.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace DnDiscord.Campaign.Services;

/// <summary>
/// Service for extracting and converting user information from authentication context.
/// </summary>
public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserContextService> _logger;

    public UserContextService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user ID from the authentication context.
    /// Converts the Discord ID (string) from JWT to a deterministic Guid.
    /// </summary>
    public Guid GetCurrentUserId()
    {
        return DiscordIdMapping.ToGuid(GetCurrentDiscordUserId());
    }

    /// <inheritdoc />
    public string GetCurrentDiscordUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user == null)
        {
            _logger.LogError("HttpContext.User is null");
            throw new UnauthorizedAccessException("User context not available");
        }

        var discordId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(discordId))
        {
            _logger.LogError("Unable to extract user ID from JWT claims");
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return discordId;
    }

    /// <inheritdoc />
    public string GetCurrentUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return string.Empty;

        return user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst("unique_name")?.Value
            ?? user.FindFirst("username")?.Value
            ?? string.Empty;
    }
}
