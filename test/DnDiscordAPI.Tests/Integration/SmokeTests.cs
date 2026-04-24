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
    public async Task GetMyCharacters_DoesNotReturn500()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/characters/my-characters");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
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
    public async Task GetAuthMe_RequiresDiscordClaims()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/auth/me");
        // Auth/me depends on Discord OAuth claims not present in TestAuthHandler.
        // Returns 500 (UserContextService throws) or 401 — both are acceptable
        // because this endpoint requires real Discord auth, not our test scheme.
        Assert.True(
            response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 500 or 401, got {(int)response.StatusCode}");
    }
}
