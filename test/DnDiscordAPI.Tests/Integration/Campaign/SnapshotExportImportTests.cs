using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DnDiscordAPI.Tests.Integration;
namespace DnDiscordAPI.Tests.Integration.Campaign;

/// <summary>
/// Integration tests for snapshot export/import functionality.
/// </summary>
[Collection("DnDiscord Integration collection")]
public sealed class SnapshotExportImportTests
{
    private readonly HttpClient _client;

    public SnapshotExportImportTests(DnDiscordIntegrationFixture fixture)
    {
        _client = fixture.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task ExportSnapshot_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/campaigns/{campaignId}/snapshots/{snapshotId}/export");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportSnapshot_InvalidJson_ReturnsBadRequest()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var request = new ImportSnapshotTestRequest(
            JsonData: "{ invalid json }",
            Label: "Imported Snapshot"
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/snapshots/import", 
            request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportSnapshot_WithValidJson_ForNonExistentCampaign_ReturnsBadRequest()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var validSnapshotJson = CreateValidSnapshotJson(campaignId);
        var request = new ImportSnapshotTestRequest(
            JsonData: validSnapshotJson,
            Label: "Imported Snapshot",
            Description: "Test import"
        );

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/snapshots/import", 
            request);

        // Assert - Campaign doesn't exist
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string CreateValidSnapshotJson(Guid campaignId)
    {
        var snapshotData = new
        {
            schemaVersion = 1,
            createdAt = DateTime.UtcNow,
            campaign = new
            {
                id = campaignId,
                name = "Test Campaign",
                description = "A test campaign",
                dungeonMasterId = Guid.NewGuid(),
                createdAt = DateTime.UtcNow,
                status = "active"
            },
            characters = Array.Empty<object>(),
            sessions = Array.Empty<object>(),
            sceneState = new
            {
                entityPositions = Array.Empty<object>(),
                camera = new { positionX = 0, positionY = 10, positionZ = 0, targetX = 0, targetY = 0, targetZ = 0, zoom = 1 },
                lighting = new { ambientIntensity = 0.5, ambientColor = "#ffffff", lightSources = Array.Empty<object>() },
                fogOfWar = new { isEnabled = false, revealedAreas = Array.Empty<object>() },
                activeEffects = Array.Empty<object>()
            },
            settings = new
            {
                rules = new { ruleSet = "5e", useEncumbrance = false, useMulticlassing = true, useFeats = true, startingLevel = 1, abilityScoreMethod = "standard_array" },
                combat = new { useInitiativeOrder = true, useGridMovement = true, gridSquareSize = 5, useOptionalFlanking = false, useAutoRoll = false, criticalHitRule = "double_dice" },
                houseRules = new { },
                enabledModules = Array.Empty<string>()
            }
        };

        return JsonSerializer.Serialize(snapshotData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

