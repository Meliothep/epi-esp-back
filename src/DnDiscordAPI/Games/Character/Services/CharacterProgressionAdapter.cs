using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;
using Multiplayer.Services;

namespace DnDiscordAPI.Games.Character.Services;

/// <summary>
/// Adapter exposing DM progression/economy actions to the Multiplayer hub.
/// XP is tracked as a lightweight in-memory remainder per character and
/// converted to level-ups with a fixed threshold.
/// </summary>
public class CharacterProgressionAdapter : ICharacterProgressionService
{
    private const int ExperiencePerLevel = 1000;
    private const int MaxBatchLevelUps = 25;

    private readonly ICharacterService _characterService;
    private readonly GamesDbContext _context;

    public CharacterProgressionAdapter(ICharacterService characterService, GamesDbContext context)
    {
        _characterService = characterService;
        _context = context;
    }

    public async Task<CharacterProgressionResult> AwardExperienceAsync(Guid characterId, int experienceAmount)
    {
        if (experienceAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(experienceAmount), "Experience amount must be > 0");

        var character = await _context.Characters.FirstOrDefaultAsync(c => c.Id == characterId)
            ?? throw new KeyNotFoundException($"Character {characterId} not found");

        var previousLevel = character.Level;
        var total = character.ExperiencePoints + experienceAmount;
        if (total / ExperiencePerLevel > MaxBatchLevelUps)
            throw new ArgumentOutOfRangeException(nameof(experienceAmount),
                $"Awarding {experienceAmount} XP would require more than {MaxBatchLevelUps} level-ups in a single batch. Split the award or use ForceLevelUpAsync.");

        var levelUps = total / ExperiencePerLevel;
        var remainder = Math.Max(0, total - (levelUps * ExperiencePerLevel));

        await using var tx = await _context.Database.BeginTransactionAsync();

        character.ExperiencePoints = remainder;
        character.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        CharacterDto after = await _characterService.GetCharacterAsync(characterId);
        for (var i = 0; i < levelUps; i++)
        {
            after = await _characterService.LevelUpAsync(characterId);
        }

        await tx.CommitAsync();

        return ToProgressionResult(
            awardedExperience: experienceAmount,
            remainder: remainder,
            previousLevel: previousLevel,
            dto: after);
    }

    public async Task<CharacterProgressionResult> ForceLevelUpAsync(Guid characterId, int levels)
    {
        if (levels <= 0)
            throw new ArgumentOutOfRangeException(nameof(levels), "Levels must be > 0");

        var before = await _characterService.GetCharacterAsync(characterId);
        var cappedLevels = Math.Min(levels, MaxBatchLevelUps);
        CharacterDto after = before;
        for (var i = 0; i < cappedLevels; i++)
        {
            after = await _characterService.LevelUpAsync(characterId);
        }

        var remainder = after.ExperiencePoints;
        return ToProgressionResult(
            awardedExperience: 0,
            remainder: remainder,
            previousLevel: before.Level,
            dto: after);
    }

    public async Task<WalletSnapshotResult> AdjustCurrencyAsync(Guid characterId, string currencyType, int amount)
    {
        if (amount == 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Currency amount must not be 0");

        var normalizedType = (currencyType ?? "gp").Trim().ToLowerInvariant();
        var request = new ModifyWalletRequest();
        switch (normalizedType)
        {
            case "cp":
                request.CopperPieces = amount;
                break;
            case "sp":
                request.SilverPieces = amount;
                break;
            case "ep":
                request.ElectrumPieces = amount;
                break;
            case "gp":
                request.GoldPieces = amount;
                break;
            case "pp":
                request.PlatinumPieces = amount;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(currencyType), "Currency type must be one of: cp, sp, ep, gp, pp");
        }

        var wallet = await _characterService.ModifyWalletAsync(characterId, request);

        return new WalletSnapshotResult
        {
            CopperPieces = wallet.CopperPieces,
            SilverPieces = wallet.SilverPieces,
            ElectrumPieces = wallet.ElectrumPieces,
            GoldPieces = wallet.GoldPieces,
            PlatinumPieces = wallet.PlatinumPieces,
            TotalInCopper = wallet.TotalInCopper,
        };
    }

    private static CharacterProgressionResult ToProgressionResult(
        int awardedExperience,
        int remainder,
        int previousLevel,
        CharacterDto dto)
    {
        return new CharacterProgressionResult
        {
            AwardedExperience = awardedExperience,
            ExperienceRemainder = remainder,
            PreviousLevel = previousLevel,
            NewLevel = dto.Level,
            LevelUps = Math.Max(0, dto.Level - previousLevel),
            CurrentHitPoints = dto.CurrentHitPoints,
            MaxHitPoints = dto.MaxHitPoints,
            ArmorClass = dto.ArmorClass,
            Initiative = dto.Initiative,
            Speed = dto.Speed,
            Strength = dto.Abilities.Strength,
            Dexterity = dto.Abilities.Dexterity,
            Constitution = dto.Abilities.Constitution,
            Intelligence = dto.Abilities.Intelligence,
            Wisdom = dto.Abilities.Wisdom,
            Charisma = dto.Abilities.Charisma,
        };
    }
}