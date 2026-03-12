namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// Represents a D&D campaign.
/// </summary>
public class Campaign
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Campaign name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Campaign description.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The Dungeon Master (owner) of this campaign.
    /// </summary>
    public Guid DungeonMasterId { get; set; }
    
    /// <summary>
    /// Campaign status.
    /// </summary>
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    
    /// <summary>
    /// When the campaign was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last time a session was played.
    /// </summary>
    public DateTime? LastPlayedAt { get; set; }
    
    /// <summary>
    /// Custom settings stored as JSON.
    /// </summary>
    public string? SettingsJson { get; set; }
    
    /// <summary>
    /// URL to the campaign's cover image.
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Maximum number of players allowed in this campaign.
    /// </summary>
    public int MaxPlayers { get; set; } = 6;
    
    /// <summary>
    /// Whether this campaign is publicly visible and joinable.
    /// </summary>
    public bool IsPublic { get; set; }
    
    /// <summary>
    /// Unique invite code for private campaigns.
    /// </summary>
    public string? InviteCode { get; set; }

    /// <summary>
    /// Unique invite code for private campaigns.
    /// </summary>
    public string? CampaignTreeDefinition { get; set; }

    /// <summary>
    /// When the invite code expires (null = never).
    /// </summary>
    public DateTime? InviteCodeExpiresAt { get; set; }
    
    /// <summary>
    /// Whether the campaign has been soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }
    
    /// <summary>
    /// When the campaign was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
    
    /// <summary>
    /// Navigation property for snapshots.
    /// </summary>
    public virtual ICollection<CampaignSnapshot> Snapshots { get; set; } = [];
    
    /// <summary>
    /// Navigation property for campaign members.
    /// </summary>
    public virtual ICollection<CampaignMember> Members { get; set; } = [];
}
