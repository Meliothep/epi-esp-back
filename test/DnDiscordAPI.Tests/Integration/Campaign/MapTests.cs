using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;

namespace DnDiscordAPI.Tests.Integration.Campaign;

/// <summary>
/// Integration tests for the campaign-scoped map persistence surface added in
/// round 2 — /api/campaigns/{id}/maps + DM-only writes via IsDungeonMasterAsync.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class MapTests
{
    private const string DmDiscordId = "444000000000000001";
    private const string StrangerDiscordId = "555000000000000002";

    private readonly DnDiscordIntegrationFixture _fixture;

    public MapTests(DnDiscordIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient Client(string discordId) => _fixture.CreateAuthenticatedClient(discordId);

    [Fact]
    public async Task CreateMap_AsDm_Returns201()
    {
        using var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        var response = await dm.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/maps",
            new { name = "Crypt Entrance", data = "{\"tiles\":[]}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<MapDtoStub>();
        Assert.NotNull(created);
        Assert.Equal("Crypt Entrance", created!.Name);
        Assert.Equal(campaignId, created.CampaignId);
    }

    [Fact]
    public async Task CreateMap_AsStranger_Returns403()
    {
        using var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        using var stranger = Client(StrangerDiscordId);
        var response = await stranger.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/maps",
            new { name = "Unauthorized", data = "{}" });

        // Strangers aren't members, so GetCampaignAsync visibility check returns null
        // before the DM auth check — the controller surfaces that as 404 via the
        // campaign-visibility guard. Either 403 or 404 is acceptable; both mean
        // "you can't write here". Lock that contract.
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden
            || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 403 or 404, got {(int)response.StatusCode} {response.StatusCode}");
    }

    [Fact]
    public async Task ListMaps_ReturnsCreatedMaps()
    {
        using var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        await dm.PostAsJsonAsync($"/api/campaigns/{campaignId}/maps",
            new { name = "Village", data = "{}" });
        await dm.PostAsJsonAsync($"/api/campaigns/{campaignId}/maps",
            new { name = "Dungeon Level 1", data = "{}" });

        var response = await dm.GetAsync($"/api/campaigns/{campaignId}/maps");
        response.EnsureSuccessStatusCode();
        var list = await response.Content.ReadFromJsonAsync<List<MapDtoStub>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, m => m.Name == "Village");
        Assert.Contains(list, m => m.Name == "Dungeon Level 1");
    }

    [Fact]
    public async Task UpdateMap_AsDm_PersistsName()
    {
        using var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        var createResponse = await dm.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/maps",
            new { name = "Draft", data = "{}" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MapDtoStub>();
        Assert.NotNull(created);

        var updateResponse = await dm.PutAsJsonAsync(
            $"/api/campaigns/{campaignId}/maps/{created!.Id}",
            new { name = "Renamed" });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<MapDtoStub>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.Name);
    }

    [Fact]
    public async Task DeleteMap_AsDm_Returns204_AndRemovesFromList()
    {
        using var dm = Client(DmDiscordId);
        var campaignId = await CreateCampaignAsync(dm);

        var createResponse = await dm.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/maps",
            new { name = "Temporary", data = "{}" });
        var created = await createResponse.Content.ReadFromJsonAsync<MapDtoStub>();
        Assert.NotNull(created);

        var deleteResponse = await dm.DeleteAsync(
            $"/api/campaigns/{campaignId}/maps/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await dm.GetAsync($"/api/campaigns/{campaignId}/maps");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<MapDtoStub>>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list!, m => m.Id == created.Id);
    }

    // ---- helpers ----

    private static async Task<Guid> CreateCampaignAsync(HttpClient client)
    {
        var body = new { name = "MapTest Campaign", description = "x", maxPlayers = 6, isPublic = true };
        var response = await client.PostAsJsonAsync("/api/campaigns", body);
        response.EnsureSuccessStatusCode();
        var campaign = await response.Content.ReadFromJsonAsync<CampaignStub>();
        Assert.NotNull(campaign);
        return campaign.Id;
    }

    private sealed record CampaignStub(Guid Id);
    private sealed record MapDtoStub(Guid Id, Guid CampaignId, string Name, string Data, DateTime CreatedAt, DateTime UpdatedAt);
}
