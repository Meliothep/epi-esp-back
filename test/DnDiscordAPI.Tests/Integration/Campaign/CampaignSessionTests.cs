using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DnDiscordAPI.Tests.Integration.Campaign;

/// <summary>
/// Integration tests for Campaign Session operations (Create / Advance / Complete).
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class CampaignSessionTests
{
    private readonly WebApplicationFactory<DnDiscordAPIProgram> _factory;
    private readonly HttpClient _client;

    public CampaignSessionTests(DnDiscordIntegrationFixture fixture)
    {
        _factory = fixture.factory!;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateSession_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var campaignId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/campaigns/{campaignId}/sessions", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListSessions_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var campaignId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/campaigns/{campaignId}/sessions");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/campaigns/{campaignId}/sessions/{sessionId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdvanceSession_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var request = new SessionAdvanceTestRequest("node-1", "story");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/sessions/{sessionId}/advance", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompleteSession_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync(
            $"/api/campaigns/{campaignId}/sessions/{sessionId}/complete", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

// Test DTOs
public record SessionAdvanceTestRequest(string NodeId, string NodeType, string? NodeTitle = null, string? PortUsed = null, string? ChoiceText = null);

public record GameSessionTestResponse(
    Guid Id,
    Guid CampaignId,
    string Status,
    string? CurrentNodeId,
    Guid StartedBy,
    DateTime StartedAt,
    DateTime? EndedAt,
    List<SessionHistoryEntryTestResponse> Entries
);

public record SessionHistoryEntryTestResponse(
    Guid Id,
    string NodeId,
    string NodeType,
    string NodeTitle,
    string? PortUsed,
    string? ChoiceText,
    DateTime VisitedAt
);

public record GameSessionListTestResponse(List<GameSessionTestResponse> Items, int TotalCount);
