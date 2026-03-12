//using System.Net;
//using System.Net.Http.Json;
//using DnDiscordAPI.Tests.Integration;
//using Microsoft.AspNetCore.Mvc.Testing;

//namespace DnDiscordAPI.Tests.Integration.Campaign;

///// <summary>
///// Integration tests for snapshot validation functionality.
///// </summary>
//[Collection("DnDiscord Integration collection")]
//public sealed class SnapshotValidationTests
//{
//    private readonly WebApplicationFactory<DnDiscordAPIProgram> _factory;
//    private readonly HttpClient _client;

//    public SnapshotValidationTests(DnDiscordIntegrationFixture fixture)
//    {
//        _factory = fixture.factory!;
//        _client = _factory.CreateClient();
//    }

//    [Fact]
//    public async Task ValidateSnapshot_NonExistent_ReturnsNotFound()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();
//        var snapshotId = Guid.NewGuid();

//        // Act
//        var response = await _client.GetAsync(
//            $"/api/campaigns/{campaignId}/snapshots/{snapshotId}/validate");

//        // Assert
//        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
//    }

//    [Fact]
//    public async Task CompareSnapshots_NonExistent_ReturnsNotFound()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();
//        var snapshotId1 = Guid.NewGuid();
//        var snapshotId2 = Guid.NewGuid();

//        // Act
//        var response = await _client.GetAsync(
//            $"/api/campaigns/{campaignId}/snapshots/compare?id1={snapshotId1}&id2={snapshotId2}");

//        // Assert
//        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
//    }

//    [Fact]
//    public async Task ListSnapshots_WithPagination_ReturnsCorrectPage()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();

//        // Act
//        var response = await _client.GetAsync(
//            $"/api/campaigns/{campaignId}/snapshots?page=1&pageSize=10");

//        // Assert
//        response.EnsureSuccessStatusCode();
//        var result = await response.Content.ReadFromJsonAsync<SnapshotListTestResponse>();
//        Assert.NotNull(result);
//        Assert.Equal(1, result.Page);
//        Assert.Equal(10, result.PageSize);
//    }

//    [Fact]
//    public async Task ListSnapshots_WithSorting_ReturnsResults()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();

//        // Act
//        var response = await _client.GetAsync(
//            $"/api/campaigns/{campaignId}/snapshots?sortBy=Version&sortDescending=true");

//        // Assert
//        response.EnsureSuccessStatusCode();
//        var result = await response.Content.ReadFromJsonAsync<SnapshotListTestResponse>();
//        Assert.NotNull(result);
//    }

//    [Fact]
//    public async Task ListSnapshots_IncludeArchived_ReturnsResults()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();

//        // Act
//        var response = await _client.GetAsync(
//            $"/api/campaigns/{campaignId}/snapshots?includeArchived=true");

//        // Assert
//        response.EnsureSuccessStatusCode();
//        var result = await response.Content.ReadFromJsonAsync<SnapshotListTestResponse>();
//        Assert.NotNull(result);
//    }
//}

