using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;

namespace DnDiscordAPI.Tests.Integration.Character;

/// <summary>
/// Integration tests for the wallet surface: GET and PATCH /api/games/character/{id}/wallet.
/// Covers all five D&D currency types (cp/sp/ep/gp/pp), the Math.Max clamp for negative
/// balances, ownership enforcement, and TotalInCopper calculation.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class WalletTests
{
    private const string OwnerDiscordId = "811000000000000011";
    private const string StrangerDiscordId = "822000000000000022";

    private readonly DnDiscordIntegrationFixture _fixture;

    public WalletTests(DnDiscordIntegrationFixture fixture) => _fixture = fixture;

    private HttpClient Client(string discordId) => _fixture.CreateAuthenticatedClient(discordId);

    // ── GET /wallet ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWallet_NewCharacter_ReturnsAllZeroBalances()
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        var response = await owner.GetAsync($"/api/games/character/{charId}/wallet");

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(0, wallet.CopperPieces);
        Assert.Equal(0, wallet.SilverPieces);
        Assert.Equal(0, wallet.ElectrumPieces);
        Assert.Equal(0, wallet.GoldPieces);
        Assert.Equal(0, wallet.PlatinumPieces);
        Assert.Equal(0, wallet.TotalInCopper);
    }

    [Fact]
    public async Task GetWallet_NonOwner_IsForbidden()
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        var stranger = Client(StrangerDiscordId);
        var response = await stranger.GetAsync($"/api/games/character/{charId}/wallet");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── PATCH /wallet ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("copperPieces",   50,  50,  0,  0,  0,  0)]
    [InlineData("silverPieces",   30,   0, 30,  0,  0,  0)]
    [InlineData("electrumPieces", 20,   0,  0, 20,  0,  0)]
    [InlineData("goldPieces",     15,   0,  0,  0, 15,  0)]
    [InlineData("platinumPieces",  5,   0,  0,  0,  0,  5)]
    public async Task ModifyWallet_EachCurrencyType_OnlyAffectsThatSlot(
        string field, int delta,
        int expCp, int expSp, int expEp, int expGp, int expPp)
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        // Build a request with only the target field set.
        var body = new Dictionary<string, int>
        {
            ["copperPieces"] = 0,
            ["silverPieces"] = 0,
            ["electrumPieces"] = 0,
            ["goldPieces"] = 0,
            ["platinumPieces"] = 0,
        };
        body[field] = delta;

        var response = await owner.PatchAsJsonAsync($"/api/games/character/{charId}/wallet", body);

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(expCp, wallet.CopperPieces);
        Assert.Equal(expSp, wallet.SilverPieces);
        Assert.Equal(expEp, wallet.ElectrumPieces);
        Assert.Equal(expGp, wallet.GoldPieces);
        Assert.Equal(expPp, wallet.PlatinumPieces);
    }

    [Fact]
    public async Task ModifyWallet_MultipleDeltas_AreAdditive()
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        // First grant: +10 gp
        await owner.PatchAsJsonAsync($"/api/games/character/{charId}/wallet",
            new { goldPieces = 10 });

        // Second grant: +5 gp more
        var response = await owner.PatchAsJsonAsync($"/api/games/character/{charId}/wallet",
            new { goldPieces = 5 });

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(15, wallet.GoldPieces);
    }

    [Fact]
    public async Task ModifyWallet_SubtractBelowZero_ClampsToZero()
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        // Character has 0 gp; subtract 100 → should stay at 0 (Math.Max clamp).
        var response = await owner.PatchAsJsonAsync($"/api/games/character/{charId}/wallet",
            new { goldPieces = -100 });

        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(0, wallet.GoldPieces);
    }

    [Fact]
    public async Task ModifyWallet_TotalInCopper_IsCalculatedCorrectly()
    {
        // 1 pp = 1000 cp, 1 gp = 100 cp, 1 ep = 50 cp, 1 sp = 10 cp, 1 cp = 1 cp
        // Grant: 1pp + 2gp + 3ep + 4sp + 5cp = 1000 + 200 + 150 + 40 + 5 = 1395 cp
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        await owner.PatchAsJsonAsync($"/api/games/character/{charId}/wallet",
            new { platinumPieces = 1, goldPieces = 2, electrumPieces = 3, silverPieces = 4, copperPieces = 5 });

        var response = await owner.GetAsync($"/api/games/character/{charId}/wallet");
        response.EnsureSuccessStatusCode();
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(1395, wallet.TotalInCopper);
    }

    [Fact]
    public async Task ModifyWallet_NonOwner_IsForbidden()
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        var stranger = Client(StrangerDiscordId);
        var response = await stranger.PatchAsJsonAsync(
            $"/api/games/character/{charId}/wallet",
            new { goldPieces = 100 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ModifyWallet_RequiresAuth()
    {
        var owner = Client(OwnerDiscordId);
        var charId = await CreateCharacterAsync(owner);

        var unauthClient = _fixture.factory!.CreateClient();
        var response = await unauthClient.PatchAsJsonAsync(
            $"/api/games/character/{charId}/wallet",
            new { goldPieces = 10 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateCharacterAsync(HttpClient client)
    {
        var body = new
        {
            name = "WalletTester",
            @class = "Guerrier",
            race = "Humain",
            abilities = new { strength = 14, dexterity = 12, constitution = 13,
                              intelligence = 10, wisdom = 10, charisma = 10 },
        };
        var response = await client.PostAsJsonAsync("/api/games/character", body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CharacterStub>();
        Assert.NotNull(result);
        return result.Id;
    }

    private sealed record WalletResponse(
        int CopperPieces, int SilverPieces, int ElectrumPieces,
        int GoldPieces, int PlatinumPieces, int TotalInCopper);

    private sealed record CharacterStub(Guid Id);
}
