using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DnDiscordAPI.Tests.Integration.Campaign;

[Collection("DnDiscord Integration collection")]
public sealed class CampaignWorkflowTests
{
    private readonly DnDiscordIntegrationFixture _fixture;

    public CampaignWorkflowTests(DnDiscordIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FullCampaignWorkflow_CreateToRestore()
    {
        var dmClient = _fixture.CreateAuthenticatedClient("dm-user");
        var playerClient = _fixture.CreateAuthenticatedClient("player-user");

        // 1. DM creates a campaign
        var createRequest = new
        {
            name = "Workflow Test Campaign",
            description = "E2E workflow test",
            maxPlayers = 6,
            isPublic = false
        };

        var createResponse = await dmClient.PostAsJsonAsync("/api/campaigns", createRequest);
        Assert.True(createResponse.IsSuccessStatusCode,
            $"Create campaign failed: {(int)createResponse.StatusCode}");

        var campaignJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var campaignId = campaignJson.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, campaignId);

        // 2. DM generates an invite code
        var inviteResponse = await dmClient.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/invite",
            new { expiresInHours = 24 });
        Assert.True(inviteResponse.IsSuccessStatusCode,
            $"Generate invite failed: {(int)inviteResponse.StatusCode}");

        var inviteJson = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>();
        var inviteCode = inviteJson.GetProperty("inviteCode").GetString();
        Assert.False(string.IsNullOrEmpty(inviteCode));

        // 3. Player joins the campaign via invite code
        var joinResponse = await playerClient.PostAsJsonAsync(
            "/api/campaigns/join",
            new { inviteCode });
        Assert.True(joinResponse.IsSuccessStatusCode,
            $"Join campaign failed: {(int)joinResponse.StatusCode}");

        // 4. DM creates a snapshot
        var snapshotRequest = new CreateSnapshotTestRequest("Workflow Snapshot", "Pre-restore backup");
        var snapshotResponse = await dmClient.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/snapshots", snapshotRequest);
        Assert.True(snapshotResponse.IsSuccessStatusCode,
            $"Create snapshot failed: {(int)snapshotResponse.StatusCode}");

        var snapshotJson = await snapshotResponse.Content.ReadFromJsonAsync<JsonElement>();
        var snapshotId = snapshotJson.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, snapshotId);

        // 5. DM exports the snapshot
        var exportResponse = await dmClient.GetAsync(
            $"/api/campaigns/{campaignId}/snapshots/{snapshotId}/export");
        Assert.True(exportResponse.IsSuccessStatusCode,
            $"Export snapshot failed: {(int)exportResponse.StatusCode}");

        // 6. DM restores the snapshot
        var restoreRequest = new RestoreSnapshotTestRequest(
            CreateBackupBeforeRestore: false,
            ValidateBeforeRestore: true);
        var restoreResponse = await dmClient.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/snapshots/{snapshotId}/restore", restoreRequest);
        Assert.True(restoreResponse.IsSuccessStatusCode,
            $"Restore snapshot failed: {(int)restoreResponse.StatusCode}");

        // 7. Verify campaign is still accessible
        var verifyResponse = await dmClient.GetAsync($"/api/campaigns/{campaignId}");
        Assert.True(verifyResponse.IsSuccessStatusCode,
            $"Verify campaign failed: {(int)verifyResponse.StatusCode}");
    }
}
