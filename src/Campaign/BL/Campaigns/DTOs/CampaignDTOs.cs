using System.ComponentModel.DataAnnotations;
using DnDiscord.Campaign.DataAccess.Models;

namespace DnDiscord.Campaign.BL.Campaigns.DTOs;

#region Campaign Requests

/// <summary>
/// Request to create a new campaign.
/// </summary>
public class CreateCampaignRequest
{
    /// <summary>
    /// Campaign name.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Campaign description.
    /// </summary>
    [StringLength(4000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// URL to the campaign's cover image.
    /// </summary>
    [StringLength(2000)]
    [Url]
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Maximum number of players (1-20).
    /// </summary>
    [Range(1, 20)]
    public int MaxPlayers { get; set; } = 6;
    
    /// <summary>
    /// Whether this campaign is publicly visible.
    /// </summary>
    public bool IsPublic { get; set; }
    
    /// <summary>
    /// Initial status of the campaign.
    /// </summary>
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
}

/// <summary>
/// Request to update an existing campaign.
/// </summary>
public class UpdateCampaignRequest
{
    /// <summary>
    /// Campaign name.
    /// </summary>
    [StringLength(200, MinimumLength = 3)]
    public string? Name { get; set; }
    
    /// <summary>
    /// Campaign description.
    /// </summary>
    [StringLength(4000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// URL to the campaign's cover image.
    /// </summary>
    [StringLength(2000)]
    [Url]
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Maximum number of players (1-20).
    /// </summary>
    [Range(1, 20)]
    public int? MaxPlayers { get; set; }
    
    /// <summary>
    /// Whether this campaign is publicly visible.
    /// </summary>
    public bool? IsPublic { get; set; }
    
    /// <summary>
    /// Campaign status.
    /// </summary>
    public CampaignStatus? Status { get; set; }
}

/// <summary>
/// Request to filter and list campaigns.
/// </summary>
public class CampaignFilterRequest
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;
    
    /// <summary>
    /// Number of items per page.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
    
    /// <summary>
    /// Search term for name/description.
    /// </summary>
    [StringLength(100)]
    public string? Search { get; set; }
    
    /// <summary>
    /// Filter by status.
    /// </summary>
    public CampaignStatus? Status { get; set; }
    
    /// <summary>
    /// Filter by visibility.
    /// </summary>
    public bool? IsPublic { get; set; }
    
    /// <summary>
    /// Filter by role (my campaigns as DM, as player, or all).
    /// </summary>
    public CampaignRoleFilter? RoleFilter { get; set; }
    
    /// <summary>
    /// Sort field.
    /// </summary>
    public CampaignSortField SortBy { get; set; } = CampaignSortField.CreatedAt;
    
    /// <summary>
    /// Sort descending.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Filter campaigns by user's role.
/// </summary>
public enum CampaignRoleFilter
{
    /// <summary>
    /// All accessible campaigns.
    /// </summary>
    All = 0,
    
    /// <summary>
    /// Campaigns where user is DM.
    /// </summary>
    AsDungeonMaster = 1,
    
    /// <summary>
    /// Campaigns where user is a player.
    /// </summary>
    AsPlayer = 2,

    /// <summary>
    /// Campaigns where user is a member (DM or player) — excludes public campaigns the user hasn't joined.
    /// </summary>
    AsMember = 3
}

/// <summary>
/// Sort fields for campaigns.
/// </summary>
public enum CampaignSortField
{
    CreatedAt,
    UpdatedAt,
    LastPlayedAt,
    Name,
    MemberCount
}

/// <summary>
/// Request to join a campaign via invite code.
/// </summary>
public class JoinCampaignRequest
{
    /// <summary>
    /// The invite code.
    /// </summary>
    [Required]
    [StringLength(20, MinimumLength = 6)]
    public string InviteCode { get; set; } = string.Empty;
}

/// <summary>
/// Request to generate a new invite code.
/// </summary>
public class GenerateInviteCodeRequest
{
    /// <summary>
    /// How long until the code expires (in hours). Null = never expires.
    /// </summary>
    [Range(1, 8760)] // Max 1 year
    public int? ExpiresInHours { get; set; }
}

#endregion

#region Campaign Responses

/// <summary>
/// Basic campaign response.
/// </summary>
public class CampaignResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DungeonMasterId { get; set; }
    public CampaignStatus Status { get; set; }
    public string? ImageUrl { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsPublic { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}

/// <summary>
/// Detailed campaign response with members.
/// </summary>
public class CampaignDetailResponse : CampaignResponse
{
    /// <summary>
    /// True when the current user (who requested the detail) is the Dungeon Master of this campaign.
    /// Allows the frontend to show "Lancer la session" without comparing Discord ID to Guid-derived DM id.
    /// </summary>
    public bool IsDungeonMaster { get; set; }
    public bool HasInviteCode { get; set; }
    public DateTime? InviteCodeExpiresAt { get; set; }
    public List<CampaignMemberResponse> Members { get; set; } = [];
    public int SnapshotCount { get; set; }
    public string? CampaignTreeDefinition { get; set; }
}

/// <summary>
/// Request to update the campaign tree (canvas nodes + connections).
/// </summary>
public class UpdateCampaignManagerRequest
{
    public string? CampaignTreeDefinition { get; set; }
}

/// <summary>
/// Paginated list of campaigns.
/// </summary>
public class CampaignListResponse
{
    public List<CampaignResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Invite code response.
/// </summary>
public class InviteCodeResponse
{
    public string InviteCode { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string JoinUrl { get; set; } = string.Empty;
}

#endregion

#region Member Requests/Responses

/// <summary>
/// Request to add a member to a campaign.
/// </summary>
public class AddMemberRequest
{
    /// <summary>
    /// User ID to add.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Role for the new member.
    /// </summary>
    public CampaignMemberRole Role { get; set; } = CampaignMemberRole.Player;
}

/// <summary>
/// Request to update a member's role or status.
/// </summary>
public class UpdateMemberRequest
{
    /// <summary>
    /// New role for the member.
    /// </summary>
    public CampaignMemberRole? Role { get; set; }
    
    /// <summary>
    /// Optional nickname.
    /// </summary>
    [StringLength(100)]
    public string? Nickname { get; set; }
    
    /// <summary>
    /// Notes about the member (DM only).
    /// </summary>
    [StringLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Campaign member response.
/// </summary>
public class CampaignMemberResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public CampaignMemberRole Role { get; set; }
    public MembershipStatus Status { get; set; }
    public string? Nickname { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

/// <summary>
/// Campaign member list response.
/// </summary>
public class CampaignMemberListResponse
{
    public List<CampaignMemberResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

#endregion

