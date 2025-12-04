namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// Represents a complete snapshot of a campaign state for backup/restore functionality.
/// </summary>
public class CampaignSnapshot
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The campaign this snapshot belongs to.
    /// </summary>
    public Guid CampaignId { get; set; }
    
    /// <summary>
    /// Auto-incrementing version number for this campaign's snapshots.
    /// </summary>
    public int Version { get; set; }
    
    /// <summary>
    /// User-friendly label for this snapshot (e.g., "Before boss fight", "Session 5 end").
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description providing more details about this snapshot.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// The user ID of whoever created this snapshot.
    /// </summary>
    public Guid CreatedBy { get; set; }
    
    /// <summary>
    /// When this snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Current status of this snapshot.
    /// </summary>
    public SnapshotStatus Status { get; set; } = SnapshotStatus.Active;
    
    /// <summary>
    /// Size of the snapshot data in bytes (for UI display and management).
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// Hash of the snapshot data for integrity verification.
    /// </summary>
    public string DataHash { get; set; } = string.Empty;
    
    /// <summary>
    /// The serialized snapshot data stored as JSON.
    /// </summary>
    public string DataJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Navigation property to the parent campaign.
    /// </summary>
    public virtual Campaign Campaign { get; set; } = null!;
}

/// <summary>
/// Status of a campaign snapshot.
/// </summary>
public enum SnapshotStatus
{
    /// <summary>
    /// Snapshot is valid and can be restored.
    /// </summary>
    Active = 0,
    
    /// <summary>
    /// Snapshot has been archived (still valid but hidden from default lists).
    /// </summary>
    Archived = 1,
    
    /// <summary>
    /// Snapshot data has been detected as corrupted.
    /// </summary>
    Corrupted = 2,
    
    /// <summary>
    /// Snapshot is being created (in progress).
    /// </summary>
    Creating = 3,
    
    /// <summary>
    /// Snapshot creation failed.
    /// </summary>
    Failed = 4
}

