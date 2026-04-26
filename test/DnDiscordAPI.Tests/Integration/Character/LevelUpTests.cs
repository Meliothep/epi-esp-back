using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;

namespace DnDiscordAPI.Tests.Integration.Character;

/// <summary>
/// Integration tests for level-up and XP field coverage.
/// Covers CharacterService.LevelUpAsync, ApplyAbilityScoreIncrease (ASI at level 4),
/// HP recalculation, and the ExperiencePoints field introduced in the progression feature.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class LevelUpTests
{
    private const string DiscordId = "911000000000000091";

    private readonly DnDiscordIntegrationFixture _fixture;

    public LevelUpTests(DnDiscordIntegrationFixture fixture) => _fixture = fixture;

    private HttpClient Client() => _fixture.CreateAuthenticatedClient(DiscordId);

    // ── ExperiencePoints initial state ────────────────────────────────────────

    [Fact]
    public async Task CreateCharacter_ExperiencePointsStartsAtZero()
    {
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Magus");

        var response = await client.GetAsync($"/api/games/character/{charId}");
        response.EnsureSuccessStatusCode();
        var character = await response.Content.ReadFromJsonAsync<FullCharacterResponse>();

        Assert.NotNull(character);
        Assert.Equal(0, character.ExperiencePoints);
    }

    // ── Level-up basic progression ────────────────────────────────────────────

    [Fact]
    public async Task LevelUp_IncreasesLevelByOne()
    {
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Brave");

        var response = await client.PostAsync($"/api/games/character/{charId}/level-up", null);

        response.EnsureSuccessStatusCode();
        var character = await response.Content.ReadFromJsonAsync<FullCharacterResponse>();
        Assert.NotNull(character);
        Assert.Equal(2, character.Level);
    }

    [Fact]
    public async Task LevelUp_IncreasesMaxHitPoints()
    {
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Hardy");

        var initial = await GetCharacterAsync(client, charId);
        var afterLevelUp = await LevelUpCharacterAsync(client, charId);

        Assert.True(afterLevelUp.MaxHitPoints > initial.MaxHitPoints,
            $"Expected MaxHP to increase after level-up. Was {initial.MaxHitPoints}, got {afterLevelUp.MaxHitPoints}");
    }

    [Fact]
    public async Task LevelUp_MultipleTimesInSequence_AccumulatesLevels()
    {
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Veteran");

        for (var i = 0; i < 3; i++)
            await LevelUpCharacterAsync(client, charId);

        var final = await GetCharacterAsync(client, charId);
        Assert.Equal(4, final.Level);
    }

    // ── Ability score increase at level 4 (ASI) ───────────────────────────────

    [Fact]
    public async Task LevelUp_Guerrier_AtLevel4_GrantsStrengthIncrease()
    {
        // Guerrier gets +2 STR every 4 levels (ApplyAbilityScoreIncrease).
        // Base strength may include racial modifiers so we compare delta, not absolute.
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Strider", @class: "Guerrier",
            strength: 14);

        var initial = await GetCharacterAsync(client, charId);

        // Level 1→2, 2→3, 3→4
        for (var i = 0; i < 3; i++)
            await LevelUpCharacterAsync(client, charId);

        var atLevel4 = await GetCharacterAsync(client, charId);
        // At level 4 the ASI fires → +2 STR (capped at 20)
        var expectedStr = Math.Min(20, initial.Abilities.Strength + 2);
        Assert.Equal(expectedStr, atLevel4.Abilities.Strength);
    }

    [Fact]
    public async Task LevelUp_AbilityScoreIncrease_CappedAt20()
    {
        // Start at STR 20 → ASI should not exceed 20.
        var client = Client();
        var charId = await CreateCharacterAsync(client, "TitanCapped", @class: "Guerrier",
            strength: 20);

        for (var i = 0; i < 3; i++)
            await LevelUpCharacterAsync(client, charId);

        var atLevel4 = await GetCharacterAsync(client, charId);
        Assert.Equal(20, atLevel4.Abilities.Strength); // clamped
    }

    [Fact]
    public async Task LevelUp_Magicien_AtLevel4_GrantsIntelligenceIncrease()
    {
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Arcanus", @class: "Magicien",
            intelligence: 16);

        var initial = await GetCharacterAsync(client, charId);

        for (var i = 0; i < 3; i++)
            await LevelUpCharacterAsync(client, charId);

        var atLevel4 = await GetCharacterAsync(client, charId);
        var expectedInt = Math.Min(20, initial.Abilities.Intelligence + 2);
        Assert.Equal(expectedInt, atLevel4.Abilities.Intelligence); // +2 INT
    }

    // ── Derived stats refreshed after level-up ────────────────────────────────

    [Fact]
    public async Task LevelUp_ArmorClassAndInitiative_ReflectDexterity()
    {
        // AC = 10 + DEX mod, Initiative = DEX mod. These are deterministic.
        var client = Client();
        var charId = await CreateCharacterAsync(client, "Nimble", @class: "Voleur",
            dexterity: 16); // DEX mod = +3 → AC 13, Init +3

        var initial = await GetCharacterAsync(client, charId);
        Assert.Equal(13, initial.ArmorClass);
        Assert.Equal(3, initial.Initiative);

        // Level up once (no ASI yet at level 2) — stats stay consistent.
        var afterLvl2 = await LevelUpCharacterAsync(client, charId);
        Assert.Equal(13, afterLvl2.ArmorClass);
        Assert.Equal(3, afterLvl2.Initiative);
    }

    // ── Guard: level-up of non-existent character ─────────────────────────────

    [Fact]
    public async Task LevelUp_NonExistentCharacter_IsForbidden()
    {
        // Ownership check runs before service lookup; non-existent IDs surface as
        // 403 (same as wallet endpoints) to prevent character-id enumeration.
        var client = Client();
        var response = await client.PostAsync($"/api/games/character/{Guid.NewGuid()}/level-up", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Ownership enforcement (regression: non-owner could level any character) ─

    [Fact]
    public async Task LevelUp_NonOwner_IsForbidden()
    {
        var owner = Client();
        var charId = await CreateCharacterAsync(owner, "OwnedHero");

        const string strangerDiscordId = "933000000000000093";
        var stranger = _fixture.CreateAuthenticatedClient(strangerDiscordId);

        var response = await stranger.PostAsync($"/api/games/character/{charId}/level-up", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LevelUp_RequiresAuth()
    {
        var owner = Client();
        var charId = await CreateCharacterAsync(owner, "AnonTarget");

        var unauthClient = _fixture.factory!.CreateClient();
        var response = await unauthClient.PostAsync($"/api/games/character/{charId}/level-up", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateCharacterAsync(
        HttpClient client,
        string name,
        string @class = "Guerrier",
        string race = "Humain",
        int strength = 14,
        int dexterity = 12,
        int constitution = 13,
        int intelligence = 10,
        int wisdom = 10,
        int charisma = 10)
    {
        var body = new
        {
            name,
            @class,
            race,
            abilities = new { strength, dexterity, constitution, intelligence, wisdom, charisma },
        };
        var response = await client.PostAsJsonAsync("/api/games/character", body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CharacterStub>();
        Assert.NotNull(result);
        return result.Id;
    }

    private static async Task<FullCharacterResponse> GetCharacterAsync(HttpClient client, Guid charId)
    {
        var response = await client.GetAsync($"/api/games/character/{charId}");
        response.EnsureSuccessStatusCode();
        var character = await response.Content.ReadFromJsonAsync<FullCharacterResponse>();
        Assert.NotNull(character);
        return character;
    }

    private static async Task<FullCharacterResponse> LevelUpCharacterAsync(HttpClient client, Guid charId)
    {
        var response = await client.PostAsync($"/api/games/character/{charId}/level-up", null);
        response.EnsureSuccessStatusCode();
        var character = await response.Content.ReadFromJsonAsync<FullCharacterResponse>();
        Assert.NotNull(character);
        return character;
    }

    private sealed record CharacterStub(Guid Id);

    private sealed record AbilitiesResponse(
        int Strength, int Dexterity, int Constitution,
        int Intelligence, int Wisdom, int Charisma);

    private sealed record FullCharacterResponse(
        Guid Id,
        string Name,
        int Level,
        int ExperiencePoints,
        int CurrentHitPoints,
        int MaxHitPoints,
        int ArmorClass,
        int Initiative,
        int Speed,
        AbilitiesResponse Abilities);
}
