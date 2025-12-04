using System.Net;
using System.Net.Http.Json;
using DnDiscordAPI.Tests.Integration;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DnDiscordAPI.Tests.Integration.Campaign;

/// <summary>
/// Integration tests for snapshot restoration functionality.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class SnapshotRestorationTests
{
    private readonly WebApplicationFactory<DnDiscordAPIProgram> _factory;
    private readonly HttpClient _client;

    public SnapshotRestorationTests(DnDiscordIntegrationFixture fixture)
    {
        _factory = fixture.factory!;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task RestoreSnapshot_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var request = new RestoreSnapshotTestRequest();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/snapshots/{snapshotId}/restore", 
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSnapshot_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/campaigns/{campaignId}/snapshots/{snapshotId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveSnapshot_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync(
            $"/api/campaigns/{campaignId}/snapshots/{snapshotId}/archive", 
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

