using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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

        // Convert Discord ID string to deterministic Guid using MD5 hash
        return ConvertDiscordIdToGuid(discordId);
    }

    /// <summary>
    /// Converts a Discord ID (string) to a deterministic Guid using MD5 hashing.
    /// </summary>
    private static Guid ConvertDiscordIdToGuid(string discordId)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(discordId));
        return new Guid(hash);
    }
}
