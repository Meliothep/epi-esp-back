using AutoMapper;
using DnDiscordAPI.Games.Character.Mappings;
using DnDiscordAPI.Games.Character.Models;
using DnDiscordAPI.Games.Character.Services;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CharModel = DnDiscordAPI.Games.Character.Models;

namespace DnDiscordAPI.Tests.Unit.Games.Character.Services;

/// <summary>
/// Unit tests for <see cref="CharacterService"/>'s ASI (Ability-Score Increase)
/// progression. Targets the idempotency guard introduced with the
/// <c>AsiAppliedCount</c> column (commit 551c5bb) so a re-entry of the level-4
/// transition cannot double-bump abilities.
/// </summary>
public class CharacterServiceTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static GamesDbContext MakeDb()
    {
        var options = new DbContextOptionsBuilder<GamesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GamesDbContext(options);
    }

    private static IMapper MakeMapper()
    {
        var cfg = new MapperConfiguration(c => c.AddProfile<CharacterMappingProfile>());
        return cfg.CreateMapper();
    }

    /// <summary>
    /// SignalRService is non-nullable on the constructor but is never touched by
    /// <see cref="CharacterService.LevelUpAsync(GamesDbContext, Guid)"/>, so
    /// passing <c>null!</c> is safe here. Building a real one would require
    /// IHubContext doubles for two hubs and adds zero coverage to ASI logic.
    /// </summary>
    private static CharacterService MakeService(GamesDbContext db)
        => new(db, MakeMapper(), null!, NullLogger<CharacterService>.Instance);

    private static CharModel.Character SeedCharacter(
        GamesDbContext db,
        int level,
        int asiAppliedCount,
        CharModel.CharacterClass cls,
        int dexterity)
    {
        var character = new CharModel.Character
        {
            Id = Guid.NewGuid(),
            DiscordUserId = "test-user",
            Name = "AsiTester",
            Class = cls,
            Race = CharacterRace.Humain,
            Level = level,
            ExperiencePoints = 0,
            AsiAppliedCount = asiAppliedCount,
            Abilities = new AbilityScores
            {
                Strength = 10,
                Dexterity = dexterity,
                Constitution = 12,
                Intelligence = 10,
                Wisdom = 10,
                Charisma = 10,
            },
            Wallet = new Wallet(),
            MaxHitPoints = 10,
            CurrentHitPoints = 10,
            ArmorClass = 10,
            Speed = 30,
            Initiative = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Characters.Add(character);
        db.SaveChanges();
        return character;
    }

    // ── ASI fires on the level-4 transition ─────────────────────────────────

    [Fact]
    public async Task LevelUpAsync_AsiFires_WhenLevelTransitionsTo4()
    {
        // Voleur is in the DEX bump branch of ApplyAbilityScoreIncrease (+2 DEX, capped at 20).
        const int initialDex = 12;
        using var db = MakeDb();
        var character = SeedCharacter(db, level: 3, asiAppliedCount: 0, cls: CharacterClass.Voleur, dexterity: initialDex);

        var service = MakeService(db);
        await service.LevelUpAsync(db, character.Id);

        var fresh = await db.Characters.FindAsync(character.Id);
        Assert.NotNull(fresh);
        Assert.Equal(4, fresh!.Level);
        Assert.Equal(1, fresh.AsiAppliedCount);
        Assert.Equal(initialDex + 2, fresh.Abilities.Dexterity);
    }

    // ── Idempotency: re-entering the level-4 transition does NOT double-bump ────

    [Fact]
    public async Task LevelUpAsync_AsiIdempotent_WhenCalledTwiceAtSameLevel()
    {
        // Repro: an admin / replay invokes LevelUpAsync twice across the level-4 boundary.
        // After the first call we manually rewind Level back to 3 (mirroring a stale snapshot
        // or buggy retry) without resetting AsiAppliedCount — that's the exact state the
        // 551c5bb idempotency guard is built to defuse. The second call must re-bump Level
        // to 4 but skip the ASI because expectedAsiCount (4/4 = 1) already matches the
        // stored AsiAppliedCount.
        const int initialDex = 12;
        using var db = MakeDb();
        var character = SeedCharacter(db, level: 3, asiAppliedCount: 0, cls: CharacterClass.Voleur, dexterity: initialDex);

        var service = MakeService(db);

        await service.LevelUpAsync(db, character.Id);
        var dexAfterFirst = (await db.Characters.FindAsync(character.Id))!.Abilities.Dexterity;
        Assert.Equal(initialDex + 2, dexAfterFirst);

        var tracked = (await db.Characters.FindAsync(character.Id))!;
        tracked.Level = 3;
        await db.SaveChangesAsync();

        await service.LevelUpAsync(db, character.Id);

        var fresh = await db.Characters.FindAsync(character.Id);
        Assert.NotNull(fresh);
        Assert.Equal(4, fresh!.Level);
        Assert.Equal(1, fresh.AsiAppliedCount);
        Assert.Equal(dexAfterFirst, fresh.Abilities.Dexterity);
    }
}
