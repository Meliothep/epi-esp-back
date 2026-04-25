namespace Multiplayer.Services;

/// <summary>
/// Multiplayer-facing service for DM-driven character progression/economy actions.
/// Implemented in DnDiscordAPI to avoid circular project references.
/// </summary>
public interface ICharacterProgressionService
{
    /// <summary>
    /// Awards XP to a character. <paramref name="expectedOwnerUserId"/> is the deterministic
    /// Discord-derived Guid the caller (hub) believes owns the character; the adapter verifies
    /// the persisted owner matches and throws <see cref="UnauthorizedAccessException"/> if not
    /// (defense-in-depth against stale SessionManager state).
    /// </summary>
    Task<CharacterProgressionResult> AwardExperienceAsync(Guid characterId, Guid expectedOwnerUserId, int experienceAmount);

    /// <summary>
    /// Forces N level-ups on the character, with the same ownership check as
    /// <see cref="AwardExperienceAsync"/>.
    /// </summary>
    Task<CharacterProgressionResult> ForceLevelUpAsync(Guid characterId, Guid expectedOwnerUserId, int levels);

    /// <summary>
    /// Adjusts a single currency slot, with the same ownership check as
    /// <see cref="AwardExperienceAsync"/>.
    /// </summary>
    Task<WalletSnapshotResult> AdjustCurrencyAsync(Guid characterId, Guid expectedOwnerUserId, string currencyType, int amount);
}

public class CharacterProgressionResult
{
    public int AwardedExperience { get; set; }
    public int ExperienceRemainder { get; set; }
    public int PreviousLevel { get; set; }
    public int NewLevel { get; set; }
    public int LevelUps { get; set; }

    public int CurrentHitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int Initiative { get; set; }
    public int Speed { get; set; }

    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }
}

public class WalletSnapshotResult
{
    public int CopperPieces { get; set; }
    public int SilverPieces { get; set; }
    public int ElectrumPieces { get; set; }
    public int GoldPieces { get; set; }
    public int PlatinumPieces { get; set; }
    public int TotalInCopper { get; set; }
}