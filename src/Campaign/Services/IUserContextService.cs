namespace DnDiscord.Campaign.Services;

/// <summary>
/// Service for extracting and converting user information from authentication context.
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Gets the current user ID from the authentication context.
    /// Converts the Discord ID (string) from JWT to a deterministic Guid.
    /// </summary>
    /// <returns>The user's Guid identifier.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID cannot be extracted from claims.</exception>
    Guid GetCurrentUserId();
}
