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

    /// <summary>
    /// Gets the raw Discord ID (string snowflake) from the JWT's sub/NameIdentifier claim.
    /// Use this to match against Character.DiscordUserId for ownership checks.
    /// </summary>
    string GetCurrentDiscordUserId();

    /// <summary>
    /// Best-effort human-readable username from JWT claims (preferred_username / name
    /// / unique_name). Returns an empty string when no display claim is present —
    /// callers should treat that as "use a fallback like 'Aventurier #shortid'".
    /// </summary>
    string GetCurrentUserName();
}
