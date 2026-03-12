//using System.Net;
//using System.Net.Http.Json;
//using DnDiscordAPI.Tests.Integration;
//using Microsoft.AspNetCore.Mvc.Testing;

//namespace DnDiscordAPI.Tests.Integration.Campaign;

///// <summary>
///// Integration tests for Campaign CRUD operations.
///// </summary>
//[Collection("DnDiscord Integration collection")]
//public sealed class CampaignCrudTests
//{
//    private readonly WebApplicationFactory<DnDiscordAPIProgram> _factory;
//    private readonly HttpClient _client;

//    public CampaignCrudTests(DnDiscordIntegrationFixture fixture)
//    {
//        _factory = fixture.factory!;
//        _client = _factory.CreateClient();
//    }

//    [Fact]
//    public async Task CreateCampaign_WithValidData_ReturnsCreated()
//    {
//        // Arrange
//        var request = new
//        {
//            name = "Test Campaign",
//            description = "A test campaign",
//            maxPlayers = 6,
//            isPublic = true
//        };

//        // Act
//        var response = await _client.PostAsJsonAsync("/api/campaigns", request);

//        // Assert
//        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
//        var result = await response.Content.ReadFromJsonAsync<CampaignTestResponse>();
//        Assert.NotNull(result);
//        Assert.Equal("Test Campaign", result.Name);
//        Assert.Equal(6, result.MaxPlayers);
//        Assert.True(result.IsPublic);
//    }

//    [Fact]
//    public async Task CreateCampaign_WithEmptyName_ReturnsBadRequest()
//    {
//        // Arrange
//        var request = new
//        {
//            name = "",
//            description = "A test campaign"
//        };

//        // Act
//        var response = await _client.PostAsJsonAsync("/api/campaigns", request);

//        // Assert
//        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
//    }

//    [Fact]
//    public async Task ListCampaigns_ReturnsOk()
//    {
//        // Act
//        var response = await _client.GetAsync("/api/campaigns");

//        // Assert
//        response.EnsureSuccessStatusCode();
//        var result = await response.Content.ReadFromJsonAsync<CampaignListTestResponse>();
//        Assert.NotNull(result);
//        Assert.NotNull(result.Items);
//    }

//    [Fact]
//    public async Task ListCampaigns_WithPagination_ReturnsCorrectPage()
//    {
//        // Act
//        var response = await _client.GetAsync("/api/campaigns?page=1&pageSize=10");

//        // Assert
//        response.EnsureSuccessStatusCode();
//        var result = await response.Content.ReadFromJsonAsync<CampaignListTestResponse>();
//        Assert.NotNull(result);
//        Assert.Equal(1, result.Page);
//        Assert.Equal(10, result.PageSize);
//    }

//    [Fact]
//    public async Task GetCampaign_NonExistent_ReturnsNotFound()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();

//        // Act
//        var response = await _client.GetAsync($"/api/campaigns/{campaignId}");

//        // Assert
//        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
//    }

//    [Fact]
//    public async Task DeleteCampaign_NonExistent_ReturnsNotFound()
//    {
//        // Arrange
//        var campaignId = Guid.NewGuid();

//        // Act
//        var response = await _client.DeleteAsync($"/api/campaigns/{campaignId}");

//        // Assert
//        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
//    }
//}

//// Test DTOs
//public record CampaignTestResponse(
//    Guid Id,
//    string Name,
//    string? Description,
//    Guid DungeonMasterId,
//    string Status,
//    string? ImageUrl,
//    int MaxPlayers,
//    bool IsPublic,
//    int MemberCount,
//    DateTime CreatedAt,
//    DateTime UpdatedAt
//);

//public record CampaignListTestResponse(
//    List<CampaignTestResponse> Items,
//    int TotalCount,
//    int Page,
//    int PageSize,
//    bool HasNextPage,
//    bool HasPreviousPage
//);

