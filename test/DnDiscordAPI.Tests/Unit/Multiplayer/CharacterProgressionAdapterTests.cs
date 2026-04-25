using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Models;
using DnDiscordAPI.Games.Character.Services;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Xunit;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

/// <summary>
/// Unit tests for CharacterProgressionAdapter — covers XP accumulation, level-up
/// triggering, currency type mapping (cp/sp/ep/gp/pp), and guard clauses.
/// Uses EF Core InMemory for the database and a hand-rolled stub for ICharacterService.
/// </summary>
public class CharacterProgressionAdapterTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static GamesDbContext MakeDb()
    {
        var options = new DbContextOptionsBuilder<GamesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated per test
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GamesDbContext(options);
    }

    private static Character SeedCharacter(GamesDbContext db, int level = 1, int xp = 0)
    {
        var character = new Character
        {
            Id = Guid.NewGuid(),
            DiscordUserId = "test-user",
            Name = "Testoric",
            Class = CharacterClass.Guerrier,
            Race = CharacterRace.Humain,
            Level = level,
            ExperiencePoints = xp,
            Abilities = new AbilityScores
            {
                Strength = 14, Dexterity = 12, Constitution = 13,
                Intelligence = 10, Wisdom = 10, Charisma = 10
            },
            Wallet = new Wallet(),
            MaxHitPoints = 10,
            CurrentHitPoints = 10,
            ArmorClass = 11,
            Speed = 30,
            Initiative = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Characters.Add(character);
        db.SaveChanges();
        return character;
    }

    private static (CharacterProgressionAdapter adapter, StubCharacterService stub, GamesDbContext db)
        MakeAdapter(int level = 1, int xp = 0)
    {
        var db = MakeDb();
        var character = SeedCharacter(db, level, xp);
        var stub = new StubCharacterService(character);
        var adapter = new CharacterProgressionAdapter(stub, db);
        return (adapter, stub, db);
    }

    // ── AwardExperience ──────────────────────────────────────────────────────

    [Fact]
    public async Task AwardExperience_BelowThreshold_DoesNotLevelUp()
    {
        var (adapter, stub, db) = MakeAdapter(level: 1, xp: 0);
        var character = db.Characters.First();

        var result = await adapter.AwardExperienceAsync(character.Id, 500);

        Assert.Equal(0, result.LevelUps);
        Assert.Equal(1, result.PreviousLevel);
        Assert.Equal(1, result.NewLevel);
        Assert.Equal(500, result.AwardedExperience);
        Assert.Equal(500, result.ExperienceRemainder);
        Assert.Equal(0, stub.LevelUpCallCount); // LevelUpAsync never called
    }

    [Fact]
    public async Task AwardExperience_AtExactThreshold_TriggersOneLevelUp()
    {
        var (adapter, stub, db) = MakeAdapter(level: 1, xp: 0);
        var character = db.Characters.First();

        var result = await adapter.AwardExperienceAsync(character.Id, 1000);

        Assert.Equal(1, result.LevelUps);
        Assert.Equal(1, stub.LevelUpCallCount);
        Assert.Equal(0, result.ExperienceRemainder); // 1000 - 1 * 1000 = 0
    }

    [Fact]
    public async Task AwardExperience_OverThreshold_AccumulatesRemainder()
    {
        var (adapter, stub, db) = MakeAdapter(level: 1, xp: 0);
        var character = db.Characters.First();

        var result = await adapter.AwardExperienceAsync(character.Id, 1700);

        Assert.Equal(1, result.LevelUps);
        Assert.Equal(700, result.ExperienceRemainder); // 1700 - 1*1000 = 700
    }

    [Fact]
    public async Task AwardExperience_WithExistingXp_CombinesTotalBeforeThreshold()
    {
        // Existing 600 XP + 600 awarded = 1200 total → 1 level-up, 200 remainder.
        var (adapter, stub, db) = MakeAdapter(level: 1, xp: 600);
        var character = db.Characters.First();

        var result = await adapter.AwardExperienceAsync(character.Id, 600);

        Assert.Equal(1, result.LevelUps);
        Assert.Equal(200, result.ExperienceRemainder);
    }

    [Fact]
    public async Task AwardExperience_MultipleLevelUps_AllTriggered()
    {
        // 3500 XP → 3 level-ups, 500 remainder.
        var (adapter, stub, db) = MakeAdapter(level: 1, xp: 0);
        var character = db.Characters.First();

        var result = await adapter.AwardExperienceAsync(character.Id, 3500);

        Assert.Equal(3, result.LevelUps);
        Assert.Equal(3, stub.LevelUpCallCount);
        Assert.Equal(500, result.ExperienceRemainder);
    }

    [Fact]
    public async Task AwardExperience_ZeroOrNegative_Throws()
    {
        var (adapter, _, db) = MakeAdapter();
        var character = db.Characters.First();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.AwardExperienceAsync(character.Id, 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.AwardExperienceAsync(character.Id, -50));
    }

    [Fact]
    public async Task AwardExperience_ExceedsBatchLimit_Throws()
    {
        // 30 000 XP at level 1 → 30 level-ups required, exceeds MaxBatchLevelUps (25).
        // Remainder (5 000) must NOT be persisted — the invariant ExperiencePoints < 1 000 would break
        // and the front-end bar would render at 500 %.
        var (adapter, _, db) = MakeAdapter(level: 1, xp: 0);
        var character = db.Characters.First();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.AwardExperienceAsync(character.Id, 30_000));

        // Character XP must be unchanged — no partial commit.
        var reloaded = await db.Characters.FindAsync(character.Id);
        Assert.Equal(0, reloaded!.ExperiencePoints);
    }

    // ── ForceLevelUp ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ForceLevelUp_CallsLevelUpNTimes()
    {
        var (adapter, stub, db) = MakeAdapter(level: 2);
        var character = db.Characters.First();

        var result = await adapter.ForceLevelUpAsync(character.Id, 3);

        Assert.Equal(3, stub.LevelUpCallCount);
        Assert.Equal(2, result.PreviousLevel); // stub always returns starting level + calls
    }

    [Fact]
    public async Task ForceLevelUp_ZeroOrNegative_Throws()
    {
        var (adapter, _, db) = MakeAdapter();
        var character = db.Characters.First();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.ForceLevelUpAsync(character.Id, 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.ForceLevelUpAsync(character.Id, -1));
    }

    [Fact]
    public async Task ForceLevelUp_ExceedsBatchLimit_Throws()
    {
        // 26 levels in one call exceeds MaxBatchLevelUps (25) — should throw, not cap.
        var (adapter, _, db) = MakeAdapter();
        var character = db.Characters.First();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.ForceLevelUpAsync(character.Id, 26));
    }

    // ── AdjustCurrency – currency type mapping ───────────────────────────────

    [Theory]
    [InlineData("cp", 50, 50, 0, 0, 0, 0)]
    [InlineData("sp", 30, 0, 30, 0, 0, 0)]
    [InlineData("ep", 20, 0, 0, 20, 0, 0)]
    [InlineData("gp", 15, 0, 0, 0, 15, 0)]
    [InlineData("pp", 5,  0, 0, 0, 0, 5)]
    public async Task AdjustCurrency_EachType_UpdatesCorrectSlotOnly(
        string currencyType, int amount,
        int expectedCp, int expectedSp, int expectedEp, int expectedGp, int expectedPp)
    {
        var (adapter, stub, db) = MakeAdapter();
        var character = db.Characters.First();

        await adapter.AdjustCurrencyAsync(character.Id, currencyType, amount);

        var req = stub.LastModifyWalletRequest!;
        Assert.Equal(expectedCp, req.CopperPieces);
        Assert.Equal(expectedSp, req.SilverPieces);
        Assert.Equal(expectedEp, req.ElectrumPieces);
        Assert.Equal(expectedGp, req.GoldPieces);
        Assert.Equal(expectedPp, req.PlatinumPieces);
    }

    [Fact]
    public async Task AdjustCurrency_UnknownType_Throws()
    {
        var (adapter, _, db) = MakeAdapter();
        var character = db.Characters.First();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.AdjustCurrencyAsync(character.Id, "xp", 10));
    }

    [Fact]
    public async Task AdjustCurrency_ZeroAmount_Throws()
    {
        var (adapter, _, db) = MakeAdapter();
        var character = db.Characters.First();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => adapter.AdjustCurrencyAsync(character.Id, "gp", 0));
    }

    [Fact]
    public async Task AdjustCurrency_ReturnsSnapshot_WithAllFields()
    {
        var (adapter, stub, db) = MakeAdapter();
        var character = db.Characters.First();

        // Stub returns fixed wallet values (see StubCharacterService)
        var result = await adapter.AdjustCurrencyAsync(character.Id, "gp", 10);

        Assert.Equal(stub.WalletResponse.CopperPieces, result.CopperPieces);
        Assert.Equal(stub.WalletResponse.SilverPieces, result.SilverPieces);
        Assert.Equal(stub.WalletResponse.ElectrumPieces, result.ElectrumPieces);
        Assert.Equal(stub.WalletResponse.GoldPieces, result.GoldPieces);
        Assert.Equal(stub.WalletResponse.PlatinumPieces, result.PlatinumPieces);
    }
}

/// <summary>
/// Minimal hand-rolled stub for ICharacterService.
/// Tracks calls and captures arguments so tests can assert on them.
/// </summary>
internal sealed class StubCharacterService : ICharacterService
{
    private readonly Character _seed;
    private int _levelUpCalls = 0;

    public int LevelUpCallCount => _levelUpCalls;
    public ModifyWalletRequest? LastModifyWalletRequest { get; private set; }
    public WalletDto WalletResponse { get; } = new()
    {
        CopperPieces = 5, SilverPieces = 10, ElectrumPieces = 2,
        GoldPieces = 20, PlatinumPieces = 1, TotalInCopper = 2715
    };

    public StubCharacterService(Character seed) => _seed = seed;

    public Task<CharacterDto> GetCharacterAsync(Guid characterId)
        => Task.FromResult(ToDto(_seed.Level));

    public Task<CharacterDto> LevelUpAsync(Guid characterId)
    {
        _levelUpCalls++;
        return Task.FromResult(ToDto(_seed.Level + _levelUpCalls));
    }

    public Task<WalletDto> ModifyWalletAsync(Guid characterId, ModifyWalletRequest request)
    {
        LastModifyWalletRequest = request;
        return Task.FromResult(WalletResponse);
    }

    // ── required interface members (not under test) ──────────────────────────

    public Task<CharacterDto> CreateCharacterAsync(string discordUserId, CreateCharacterRequest request)
        => throw new NotImplementedException();

    public Task<List<CharacterDto>> GetUserCharactersAsync(string discordUserId)
        => throw new NotImplementedException();

    public Task<CharacterDto> UpdateHitPointsAsync(Guid characterId, int newHitPoints)
        => throw new NotImplementedException();

    public Task<WalletDto> GetWalletAsync(Guid characterId)
        => throw new NotImplementedException();

    public Task<string?> GetOwnerDiscordIdAsync(Guid characterId)
        => throw new NotImplementedException();

    // ── private helpers ──────────────────────────────────────────────────────

    private CharacterDto ToDto(int level) => new()
    {
        Id = _seed.Id,
        Name = _seed.Name,
        Level = level,
        ExperiencePoints = _seed.ExperiencePoints,
        Class = (CharacterClass)_seed.Class,
        Race = (CharacterRace)_seed.Race,
        CurrentHitPoints = _seed.CurrentHitPoints,
        MaxHitPoints = _seed.MaxHitPoints,
        ArmorClass = _seed.ArmorClass,
        Initiative = _seed.Initiative,
        Speed = _seed.Speed,
        Abilities = new AbilityScoresDto
        {
            Strength = _seed.Abilities.Strength,
            Dexterity = _seed.Abilities.Dexterity,
            Constitution = _seed.Abilities.Constitution,
            Intelligence = _seed.Abilities.Intelligence,
            Wisdom = _seed.Abilities.Wisdom,
            Charisma = _seed.Abilities.Charisma,
        },
        Wallet = WalletResponse,
        RaceTraits = new RaceTraitsDto
        {
            StrengthModifier = 0, DexterityModifier = 0, ConstitutionModifier = 0,
            IntelligenceModifier = 0, WisdomModifier = 0, CharismaModifier = 0,
            BaseSpeed = 30, SpecialAbilities = [],
        },
        ClassTraits = new ClassTraitsDto
        {
            MainCharacteristic = "Strength", HitDie = "d10", SavingThrows = [],
            Proficiencies = [], IsSpellcaster = false, SpecialFeatures = [],
        },
    };
}
