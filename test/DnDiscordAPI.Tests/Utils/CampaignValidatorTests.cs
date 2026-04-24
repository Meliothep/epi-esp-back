using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.BL.Campaigns.DTOs;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.Extensions.Logging.Abstractions;
using CampaignEntity = DnDiscord.Campaign.DataAccess.Models.Campaign;

namespace DnDiscordAPI.Tests.Utils;

public sealed class CampaignValidatorTests
{
    private readonly CampaignValidator _validator = new(NullLogger<CampaignValidator>.Instance);

    private static CreateCampaignRequest ValidCreateRequest() => new()
    {
        Name = "Test Campaign",
        Description = "A valid description",
        MaxPlayers = 6,
        Status = CampaignStatus.Draft
    };

    private static CampaignEntity CreateCampaign(Guid dmId, bool isPublic = false) => new()
    {
        Id = Guid.NewGuid(),
        DungeonMasterId = dmId,
        Name = "Test",
        IsPublic = isPublic
    };

    // --- ValidateCreate tests ---

    [Fact]
    public void ValidateCreate_ValidRequest_ReturnsSuccess()
    {
        var result = _validator.ValidateCreate(ValidCreateRequest());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateCreate_EmptyName_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.Name = "";
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "REQUIRED");
    }

    [Fact]
    public void ValidateCreate_NameTooShort_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.Name = "AB";
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOO_SHORT");
    }

    [Fact]
    public void ValidateCreate_NameTooLong_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.Name = new string('A', 201);
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOO_LONG");
    }

    [Fact]
    public void ValidateCreate_DescriptionTooLong_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.Description = new string('A', 4001);
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "TOO_LONG");
    }

    [Fact]
    public void ValidateCreate_MaxPlayersOutOfRange_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.MaxPlayers = 0;
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "OUT_OF_RANGE");
    }

    [Fact]
    public void ValidateCreate_MaxPlayersAboveMax_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.MaxPlayers = 21;
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "OUT_OF_RANGE");
    }

    [Fact]
    public void ValidateCreate_InvalidImageUrl_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.ImageUrl = "not-a-url";
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_URL");
    }

    [Fact]
    public void ValidateCreate_ArchivedStatus_ReturnsError()
    {
        var request = ValidCreateRequest();
        request.Status = CampaignStatus.Archived;
        var result = _validator.ValidateCreate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_STATUS");
    }

    // --- ValidateUpdate tests ---

    [Fact]
    public void ValidateUpdate_NullFields_ReturnsSuccess()
    {
        var request = new UpdateCampaignRequest();
        var result = _validator.ValidateUpdate(request);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateUpdate_EmptyName_ReturnsError()
    {
        var request = new UpdateCampaignRequest { Name = "" };
        var result = _validator.ValidateUpdate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "REQUIRED");
    }

    // --- Permission tests ---

    [Fact]
    public void CanModify_DM_ReturnsTrue()
    {
        var dmId = Guid.NewGuid();
        var campaign = CreateCampaign(dmId);
        Assert.True(_validator.CanModify(campaign, dmId));
    }

    [Fact]
    public void CanModify_NonDM_ReturnsFalse()
    {
        var dmId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var campaign = CreateCampaign(dmId);
        Assert.False(_validator.CanModify(campaign, otherId));
    }

    [Fact]
    public void CanView_PublicCampaign_ReturnsTrue()
    {
        var dmId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var campaign = CreateCampaign(dmId, isPublic: true);
        Assert.True(_validator.CanView(campaign, otherId, isMember: false));
    }

    [Fact]
    public void CanView_PrivateCampaign_NonMemberCannotView()
    {
        var dmId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var campaign = CreateCampaign(dmId, isPublic: false);
        Assert.False(_validator.CanView(campaign, otherId, isMember: false));
    }

    [Fact]
    public void CanManageMembers_OnlyDM()
    {
        var dmId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var campaign = CreateCampaign(dmId);
        Assert.True(_validator.CanManageMembers(campaign, dmId));
        Assert.False(_validator.CanManageMembers(campaign, otherId));
    }
}
