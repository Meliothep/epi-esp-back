namespace Multiplayer.Services;

/// <summary>
/// Normalises and validates currency type tokens received from hub payloads.
/// Centralising this logic makes the hub thin and the rule easily testable.
/// </summary>
public static class CurrencyTypeHelper
{
    private static readonly string[] ValidTypes = { "cp", "sp", "ep", "gp", "pp" };

    /// <summary>
    /// Trims whitespace, lowercases, and falls back to "gp" when <paramref name="raw"/> is null.
    /// </summary>
    public static string Normalize(string? raw) => (raw ?? "gp").Trim().ToLowerInvariant();

    /// <summary>Returns true when <paramref name="normalized"/> is one of: cp, sp, ep, gp, pp.</summary>
    public static bool IsValid(string normalized) => Array.IndexOf(ValidTypes, normalized) >= 0;
}
