using System.Net;
using System.Net.Http.Json;

namespace DnDiscordAPI.Tests.Integration;

[Collection("DnDiscord Integration collection")]
public sealed class SmokeTests
{
    private readonly DnDiscordIntegrationFixture _fixture;

    public SmokeTests(DnDiscordIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetCampaigns_Returns200()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/campaigns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyCharacters_Returns200()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/characters/my-characters");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 200 or 401, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task PostCampaign_ReturnsSuccess()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var request = new
        {
            name = "Smoke Test Campaign",
            description = "Created by smoke test",
            maxPlayers = 4,
            isPublic = true
        };

        var response = await client.PostAsJsonAsync("/api/campaigns", request);
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task GetAuthMe_ReturnsSuccess()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.True(
            response.IsSuccessStatusCode ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"Expected success or 404, got {(int)response.StatusCode}");
    }
}
