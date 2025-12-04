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
    /// Navigation property for snapshots.
    /// </summary>
    public virtual ICollection<CampaignSnapshot> Snapshots { get; set; } = [];
}

