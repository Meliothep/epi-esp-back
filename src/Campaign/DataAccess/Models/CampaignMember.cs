namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// Represents a member (player) in a campaign.
/// </summary>
public class CampaignMember
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The campaign this member belongs to.
    /// </summary>
    public Guid CampaignId { get; set; }
    
    /// <summary>
    /// The user ID of the member.
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// The member's role in the campaign.
    /// </summary>
    public CampaignMemberRole Role { get; set; } = CampaignMemberRole.Player;
    
    /// <summary>
    /// Current status of the membership.
    /// </summary>
    public MembershipStatus Status { get; set; } = MembershipStatus.Pending;
    
    /// <summary>
    /// When the member joined or was invited.
    /// </summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the invitation was accepted (if applicable).
    /// </summary>
    public DateTime? AcceptedAt { get; set; }
    
    /// <summary>
    /// Optional nickname for the member in this campaign.
    /// </summary>
    public string? Nickname { get; set; }
    
    /// <summary>
    /// Optional notes about this member (visible to DM only).
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// Navigation property to the campaign.
    /// </summary>
    public virtual Campaign Campaign { get; set; } = null!;
}

/// <summary>
/// Role of a member in a campaign.
/// </summary>
public enum CampaignMemberRole
{
    /// <summary>
    /// Regular player.
    /// </summary>
    Player = 0,
    
    /// <summary>
    /// Co-Dungeon Master (can help manage the campaign).
    /// </summary>
    CoDungeonMaster = 1,
    
    /// <summary>
    /// Spectator (can view but not participate).
    /// </summary>
    Spectator = 2
}

/// <summary>
/// Status of a campaign membership.
/// </summary>
public enum MembershipStatus
{
    /// <summary>
    /// Invitation sent, waiting for acceptance.
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Member has accepted and is active.
    /// </summary>
    Active = 1,
    
    /// <summary>
    /// Member declined the invitation.
    /// </summary>
    Declined = 2,
    
    /// <summary>
    /// Member was removed by the DM.
    /// </summary>
    Removed = 3,
    
    /// <summary>
    /// Member left voluntarily.
    /// </summary>
    Left = 4
}

