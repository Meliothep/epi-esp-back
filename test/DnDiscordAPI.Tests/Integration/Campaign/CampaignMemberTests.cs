using System.Net;
using System.Net.Http.Json;

namespace DnDiscordAPI.Tests.Integration.Campaign;

[Collection("DnDiscord Integration collection")]
public sealed class CampaignMemberTests
{
    private readonly DnDiscordIntegrationFixture _fixture;

    public CampaignMemberTests(DnDiscordIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InviteAndJoinFlow_WorksEndToEnd()
    {
        var dmClient = _fixture.CreateAuthenticatedClient("dm-member-test");
        var playerClient = _fixture.CreateAuthenticatedClient("player-member-test");

        // Step 1: DM creates a campaign
        var createRequest = new
        {
            name = "Member Test Campaign",
            description = "Testing invite flow",
            maxPlayers = 6,
            isPublic = true
        };
        var createResponse = await dmClient.PostAsJsonAsync("/api/campaigns", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var campaign = await createResponse.Content.ReadFromJsonAsync<CampaignDetailTestResponse>();
        Assert.NotNull(campaign);
        var campaignId = campaign.Id;

        // Step 2: DM generates an invite code
        var inviteResponse = await dmClient.PostAsJsonAsync(
            $"/api/campaigns/{campaignId}/invite",
            new { });
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<InviteCodeTestResponse>();
        Assert.NotNull(invite);
        Assert.False(string.IsNullOrEmpty(invite.InviteCode));

        // Step 3: Player joins with invite code
        var joinResponse = await playerClient.PostAsJsonAsync(
            "/api/campaigns/join",
            new { inviteCode = invite.InviteCode });
        joinResponse.EnsureSuccessStatusCode();

        // Step 4: List members — DM is not stored as a member, only the joined player
        var membersResponse = await dmClient.GetAsync($"/api/campaigns/{campaignId}/members");
        membersResponse.EnsureSuccessStatusCode();
        var members = await membersResponse.Content.ReadFromJsonAsync<MemberListTestResponse>();
        Assert.NotNull(members);
        Assert.Equal(1, members.TotalCount);

        // Step 5: Player leaves
        var leaveResponse = await playerClient.PostAsync(
            $"/api/campaigns/{campaignId}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, leaveResponse.StatusCode);

        // Step 6: List members — should be empty (DM is tracked via Campaign.DungeonMasterId, not as member)
        var membersAfterLeave = await dmClient.GetAsync($"/api/campaigns/{campaignId}/members");
        membersAfterLeave.EnsureSuccessStatusCode();
        var membersLeft = await membersAfterLeave.Content.ReadFromJsonAsync<MemberListTestResponse>();
        Assert.NotNull(membersLeft);
        Assert.Equal(0, membersLeft.TotalCount);
    }

    [Fact]
    public async Task GenerateInviteCode_AsNonDm_ReturnsBadRequest()
    {
        var dmClient = _fixture.CreateAuthenticatedClient("dm-invite-auth-test");
        var otherClient = _fixture.CreateAuthenticatedClient("other-invite-auth-test");

        // DM creates campaign
        var createResponse = await dmClient.PostAsJsonAsync("/api/campaigns", new
        {
            name = "Invite Auth Test",
            description = "Test",
            maxPlayers = 4,
            isPublic = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var campaign = await createResponse.Content.ReadFromJsonAsync<CampaignDetailTestResponse>();
        Assert.NotNull(campaign);

        // Non-DM tries to generate invite code — should fail
        var inviteResponse = await otherClient.PostAsJsonAsync(
            $"/api/campaigns/{campaign.Id}/invite",
            new { });
        Assert.True(
            inviteResponse.StatusCode == HttpStatusCode.BadRequest ||
            inviteResponse.StatusCode == HttpStatusCode.Forbidden ||
            inviteResponse.StatusCode == HttpStatusCode.NotFound,
            $"Expected 400/403/404 but got {inviteResponse.StatusCode}");
    }
}

// Test DTOs for member tests
public record CampaignDetailTestResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid DungeonMasterId,
    int Status,
    int MaxPlayers,
    bool IsPublic,
    int MemberCount,
    bool IsDungeonMaster
);

public record InviteCodeTestResponse(
    string InviteCode,
    DateTime? ExpiresAt,
    string JoinUrl
);

public record MemberTestResponse(
    Guid Id,
    Guid UserId,
    int Role,
    int Status,
    string? Nickname,
    DateTime JoinedAt
);

public record MemberListTestResponse(
    List<MemberTestResponse> Items,
    int TotalCount
);
