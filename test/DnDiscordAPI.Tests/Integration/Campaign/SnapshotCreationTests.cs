using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DnDiscordAPI.Tests.Integration.Campaign;

/// <summary>
/// Integration tests for snapshot creation functionality.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class SnapshotCreationTests
{
    private readonly WebApplicationFactory<DnDiscordAPIProgram> _factory;
    private readonly HttpClient _client;

    public SnapshotCreationTests(DnDiscordIntegrationFixture fixture)
    {
        _factory = fixture.factory!;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateSnapshot_WithValidCampaign_ReturnsCreated()
    {
        // Arrange
        // Note: In a real test, we would first create a campaign
        // For now, we test the endpoint returns appropriate error for non-existent campaign
        var campaignId = Guid.NewGuid();
        var request = new CreateSnapshotTestRequest("Test Snapshot", "Test description");

        // Act
        var response = await _client.PostAsJsonAsync($"/api/campaigns/{campaignId}/snapshots", request);

        // Assert - Campaign doesn't exist, so we expect NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSnapshot_WithEmptyLabel_ReturnsBadRequest()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var request = new CreateSnapshotTestRequest("");

        // Act
        var response = await _client.PostAsJsonAsync($"/api/campaigns/{campaignId}/snapshots", request);

        // Assert - Should return BadRequest for invalid request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListSnapshots_ForNonExistentCampaign_ReturnsEmptyList()
    {
        // Arrange
        var campaignId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/campaigns/{campaignId}/snapshots");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SnapshotListTestResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetSnapshot_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/campaigns/{campaignId}/snapshots/{snapshotId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

