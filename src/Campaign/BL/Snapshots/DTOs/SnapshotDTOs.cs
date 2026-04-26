using System.ComponentModel.DataAnnotations;
using DnDiscord.Campaign.DataAccess.Models;

namespace DnDiscord.Campaign.BL.Snapshots.DTOs;

/// <summary>
/// Request to create a new campaign snapshot.
/// </summary>
public class CreateSnapshotRequest
{
    /// <summary>
    /// User-friendly label for the snapshot.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description providing more context.
    /// </summary>
    [StringLength(2000)]
    public string? Description { get; set; }
}

/// <summary>
/// Request to restore a campaign from a snapshot.
/// </summary>
public class RestoreSnapshotRequest
{
    /// <summary>
    /// If true, creates a backup of current state before restoring.
    /// </summary>
    public bool CreateBackupBeforeRestore { get; set; } = true;
    
    /// <summary>
    /// If true, validates the snapshot before restoring.
    /// </summary>
    public bool ValidateBeforeRestore { get; set; } = true;
    
    /// <summary>
    /// Label for the automatic backup (if CreateBackupBeforeRestore is true).
    /// </summary>
    [StringLength(200)]
    public string? BackupLabel { get; set; }
}

/// <summary>
/// Request to import a snapshot from JSON.
/// </summary>
public class ImportSnapshotRequest
{
    /// <summary>
    /// The JSON data to import.
    /// </summary>
    [Required]
    public string JsonData { get; set; } = string.Empty;
    
    /// <summary>
    /// Label for the imported snapshot.
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description.
    /// </summary>
    [StringLength(2000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// If true, validates the imported data before saving.
    /// </summary>
    public bool ValidateImport { get; set; } = true;
}

/// <summary>
/// Response containing snapshot metadata.
/// </summary>
public class SnapshotResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public int Version { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public SnapshotStatus Status { get; set; }
    public long SizeBytes { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    
    /// <summary>
    /// Summary of snapshot contents.
    /// </summary>
    public SnapshotContentSummary? ContentSummary { get; set; }
}

/// <summary>
/// Summary of what's contained in a snapshot.
/// </summary>
public class SnapshotContentSummary
{
    public string CampaignName { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public int SessionCount { get; set; }
    public bool HasSceneState { get; set; }
    public DateTime? LastSessionDate { get; set; }
}

/// <summary>
/// Response for listing snapshots with pagination.
/// </summary>
public class SnapshotListResponse
{
    public List<SnapshotResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

/// <summary>
/// Request parameters for listing snapshots.
/// </summary>
public class ListSnapshotsRequest
{
    public int Page { get; set; } = 1;
    
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
    
    /// <summary>
    /// Filter by status (null = all statuses except Corrupted and Failed).
    /// </summary>
    public SnapshotStatus? Status { get; set; }
    
    /// <summary>
    /// Include archived snapshots.
    /// </summary>
    public bool IncludeArchived { get; set; }
    
    /// <summary>
    /// Sort by field.
    /// </summary>
    public SnapshotSortField SortBy { get; set; } = SnapshotSortField.CreatedAt;
    
    /// <summary>
    /// Sort direction.
    /// </summary>
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Fields available for sorting snapshots.
/// </summary>
public enum SnapshotSortField
{
    CreatedAt,
    Version,
    Label,
    SizeBytes
}

/// <summary>
/// Response for snapshot restoration.
/// </summary>
public class RestoreSnapshotResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the backup snapshot created before restoration (if requested).
    /// </summary>
    public Guid? BackupSnapshotId { get; set; }
    
    /// <summary>
    /// Warnings encountered during restoration.
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Response for snapshot export (JSON download).
/// </summary>
public class ExportSnapshotResponse
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/json";
    public string JsonData { get; set; } = string.Empty;
}

/// <summary>
/// Response for snapshot comparison.
/// </summary>
public class CompareSnapshotsResponse
{
    public Guid Snapshot1Id { get; set; }
    public Guid Snapshot2Id { get; set; }
    public List<SnapshotDifference> Differences { get; set; } = [];
    public ComparisonSummary Summary { get; set; } = new();
}

/// <summary>
/// Individual difference between two snapshots.
/// </summary>
public class SnapshotDifference
{
    public string Category { get; set; } = string.Empty; // "Campaign", "Characters", "Sessions", "SceneState", "Settings"
    public string Field { get; set; } = string.Empty;
    public DifferenceType Type { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// Type of difference.
/// </summary>
public enum DifferenceType
{
    Added,
    Removed,
    Modified
}

/// <summary>
/// Summary of snapshot comparison.
/// </summary>
public class ComparisonSummary
{
    public int AddedCount { get; set; }
    public int RemovedCount { get; set; }
    public int ModifiedCount { get; set; }
    public int TotalDifferences { get; set; }
}

/// <summary>
/// Response for validation results.
/// </summary>
public class ValidationResultResponse
{
    public bool IsValid { get; set; }
    public List<ValidationErrorResponse> Errors { get; set; } = [];
    public List<ValidationWarningResponse> Warnings { get; set; } = [];
}

/// <summary>
/// Validation error in response format.
/// </summary>
public class ValidationErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Field { get; set; }
}

/// <summary>
/// Validation warning in response format.
/// </summary>
public class ValidationWarningResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Field { get; set; }
}

