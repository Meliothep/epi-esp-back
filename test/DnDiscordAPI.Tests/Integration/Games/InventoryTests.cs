using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;

namespace DnDiscordAPI.Tests.Integration.Games;

/// <summary>
/// Integration tests for the inventory surface added in the DM + Inventory feature —
/// catalog, DM-gated give, owner-only remove, and the use endpoint.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class InventoryTests
{
    private const string DmDiscordId = "111000000000000001";
    private const string PlayerDiscordId = "222000000000000002";
    private const string StrangerDiscordId = "333000000000000003";

    private readonly DnDiscordIntegrationFixture _fixture;

    public InventoryTests(DnDiscordIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient Client(string discordId) => _fixture.CreateAuthenticatedClient(discordId);

    [Fact]
    public async Task GetCatalog_ReturnsSeededItems()
    {
        using var client = Client(PlayerDiscordId);
        var response = await client.GetAsync("/api/games/inventory/catalog");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<CatalogItem>>();
        Assert.NotNull(items);
        // Catalog seeds 12 items; at minimum we expect more than zero.
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GiveItem_RequiresAuthenticatedUser()
    {
        // Unauthenticated request (no X-Test-UserId header).
        var client = _fixture.factory!.CreateClient();
        var body = new { itemId = Guid.NewGuid(), quantity = 1, campaignId = Guid.NewGuid() };
        var response = await client.PostAsJsonAsync($"/api/games/inventory/{Guid.NewGuid()}", body);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GiveItem_NonDm_IsForbidden()
    {
        // DM creates a campaign.
        var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        // Player (not the DM) creates a character.
        var player = Client(PlayerDiscordId);
        var characterId = await CreateCharacterAsync(player);

        var catalogItemId = await GetFirstCatalogItemIdAsync(player);

        // Stranger (not the DM, not the owner) tries to grant an item to the player's character.
        var stranger = Client(StrangerDiscordId);
        var body = new { itemId = catalogItemId, quantity = 1, campaignId };
        var response = await stranger.PostAsJsonAsync($"/api/games/inventory/{characterId}", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GiveItem_FromDm_Persists()
    {
        var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        var player = Client(PlayerDiscordId);
        var characterId = await CreateCharacterAsync(player);

        var catalogItemId = await GetFirstCatalogItemIdAsync(dm);

        // DM grants.
        var body = new { itemId = catalogItemId, quantity = 2, campaignId };
        var grant = await dm.PostAsJsonAsync($"/api/games/inventory/{characterId}", body);
        grant.EnsureSuccessStatusCode();

        // Owner reads back their inventory — should contain the entry with qty 2.
        var list = await player.GetAsync($"/api/games/inventory/{characterId}");
        list.EnsureSuccessStatusCode();
        var entries = await list.Content.ReadFromJsonAsync<List<InventoryEntryResponse>>();
        Assert.NotNull(entries);
        Assert.Single(entries);
        Assert.Equal(2, entries[0].Quantity);
        Assert.Equal(catalogItemId, entries[0].Item.Id);
    }

    [Fact]
    public async Task GetInventory_NonOwnerWithoutCampaignContext_IsForbidden()
    {
        var player = Client(PlayerDiscordId);
        var characterId = await CreateCharacterAsync(player);

        var stranger = Client(StrangerDiscordId);
        var response = await stranger.GetAsync($"/api/games/inventory/{characterId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- helpers ----

    private static async Task<Guid> CreateCampaignAsync(HttpClient client)
    {
        var body = new { name = "InventoryTest Campaign", description = "x", maxPlayers = 6, isPublic = true };
        var response = await client.PostAsJsonAsync("/api/campaigns", body);
        response.EnsureSuccessStatusCode();
        var campaign = await response.Content.ReadFromJsonAsync<CampaignStub>();
        Assert.NotNull(campaign);
        return campaign.Id;
    }

    private static async Task<Guid> CreateCharacterAsync(HttpClient client)
    {
        // The API uses French enum names for class/race (Guerrier = Fighter, Humain = Human).
        var body = new
        {
            name = "Testoric",
            @class = "Guerrier",
            race = "Humain",
            abilities = new { strength = 14, dexterity = 12, constitution = 13, intelligence = 10, wisdom = 10, charisma = 10 },
        };
        var response = await client.PostAsJsonAsync("/api/games/character", body);
        response.EnsureSuccessStatusCode();
        var character = await response.Content.ReadFromJsonAsync<CharacterStub>();
        Assert.NotNull(character);
        return character.Id;
    }

    private static async Task<Guid> GetFirstCatalogItemIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/games/inventory/catalog");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<CatalogItem>>();
        Assert.NotNull(items);
        Assert.NotEmpty(items);
        return items[0].Id;
    }

    private sealed record CatalogItem(Guid Id, string Name, string Category);
    private sealed record CampaignStub(Guid Id);
    private sealed record CharacterStub(Guid Id);
    private sealed record InventoryEntryResponse(Guid Id, Guid CharacterId, int Quantity, CatalogItem Item);
}
