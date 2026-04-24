namespace Multiplayer.Services;

/// <summary>
/// Multiplayer-facing service for DM-driven character progression/economy actions.
/// Implemented in DnDiscordAPI to avoid circular project references.
/// </summary>
public interface ICharacterProgressionService
{
    Task<CharacterProgressionResult> AwardExperienceAsync(Guid characterId, int experienceAmount);
    Task<CharacterProgressionResult> ForceLevelUpAsync(Guid characterId, int levels);
    Task<WalletSnapshotResult> AdjustCurrencyAsync(Guid characterId, string currencyType, int amount);
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