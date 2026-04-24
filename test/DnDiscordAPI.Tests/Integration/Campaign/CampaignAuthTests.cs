using System.Net;
using System.Net.Http.Json;

namespace DnDiscordAPI.Tests.Integration.Campaign;

[Collection("DnDiscord Integration collection")]
public sealed class CampaignAuthTests
{
    private readonly DnDiscordIntegrationFixture _fixture;

    public CampaignAuthTests(DnDiscordIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateCampaign_WithoutAuth_Fails()
    {
        var unauthClient = _fixture.factory!.CreateClient();
        var request = new { name = "Unauthorized Campaign", description = "Should fail" };

        var response = await unauthClient.PostAsJsonAsync("/api/campaigns", request);

        // Campaign controller doesn't have [Authorize] — it throws when extracting user ID
        Assert.False(response.IsSuccessStatusCode,
            $"Expected non-success status but got {response.StatusCode}");
    }

    [Fact]
    public async Task ListCampaigns_WithoutAuth_Fails()
    {
        var unauthClient = _fixture.factory!.CreateClient();

        var response = await unauthClient.GetAsync("/api/campaigns");

        Assert.False(response.IsSuccessStatusCode,
            $"Expected non-success status but got {response.StatusCode}");
    }

    [Fact]
    public async Task Campaigns_AreIsolatedByUser()
    {
        var clientA = _fixture.CreateAuthenticatedClient("user-a-isolation-test");
        var clientB = _fixture.CreateAuthenticatedClient("user-b-isolation-test");

        // User A creates a campaign
        var createRequest = new
        {
            name = "User A Secret Campaign",
            description = "Only for User A",
            maxPlayers = 4,
            isPublic = false
        };
        var createResponse = await clientA.PostAsJsonAsync("/api/campaigns", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // User A can see it in their list
        var listA = await clientA.GetAsync("/api/campaigns");
        listA.EnsureSuccessStatusCode();
        var campaignsA = await listA.Content.ReadFromJsonAsync<CampaignListTestResponse>();
        Assert.NotNull(campaignsA);
        Assert.Contains(campaignsA.Items, c => c.Name == "User A Secret Campaign");

        // User B should NOT see User A's private campaign
        var listB = await clientB.GetAsync("/api/campaigns");
        listB.EnsureSuccessStatusCode();
        var campaignsB = await listB.Content.ReadFromJsonAsync<CampaignListTestResponse>();
        Assert.NotNull(campaignsB);
        Assert.DoesNotContain(campaignsB.Items, c => c.Name == "User A Secret Campaign");
    }
}
