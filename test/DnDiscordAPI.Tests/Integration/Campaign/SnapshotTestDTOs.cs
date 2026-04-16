namespace DnDiscordAPI.Tests.Integration.Campaign;

/// <summary>
/// DTOs for snapshot integration tests.
/// </summary>
public record CreateSnapshotTestRequest(string Label, string? Description = null);

public record SnapshotTestResponse(
    Guid Id,
    Guid CampaignId,
    int Version,
    string Label,
    string? Description,
    Guid CreatedBy,
    DateTime CreatedAt,
    int Status,
    long SizeBytes,
    string SizeFormatted,
    SnapshotContentSummaryTest? ContentSummary
);

public record SnapshotContentSummaryTest(
    string CampaignName,
    int CharacterCount,
    int SessionCount,
    bool HasSceneState,
    DateTime? LastSessionDate
);

public record SnapshotListTestResponse(
    List<SnapshotTestResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage
);

public record RestoreSnapshotTestRequest(
    bool CreateBackupBeforeRestore = true,
    bool ValidateBeforeRestore = true,
    string? BackupLabel = null
);

public record RestoreSnapshotTestResponse(
    bool Success,
    string Message,
    Guid? BackupSnapshotId,
    List<string> Warnings
);

public record ImportSnapshotTestRequest(
    string JsonData,
    string Label,
    string? Description = null,
    bool ValidateImport = true
);

public record ValidationResultTestResponse(
    bool IsValid,
    List<ValidationErrorTestResponse> Errors,
    List<ValidationWarningTestResponse> Warnings
);

public record ValidationErrorTestResponse(string Code, string Message, string? Field);
public record ValidationWarningTestResponse(string Code, string Message, string? Field);

public record CompareSnapshotsTestResponse(
    Guid Snapshot1Id,
    Guid Snapshot2Id,
    List<SnapshotDifferenceTest> Differences,
    ComparisonSummaryTest Summary
);

public record SnapshotDifferenceTest(
    string Category,
    string Field,
    string Type,
    string? OldValue,
    string? NewValue
);

public record ComparisonSummaryTest(
    int AddedCount,
    int RemovedCount,
    int ModifiedCount,
    int TotalDifferences
);

