using DnDiscord.Campaign.BL.Snapshots.DTOs;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.BL.Snapshots;

/// <summary>
/// Service for managing campaign snapshots (backup/restore functionality).
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Creates a new snapshot of the campaign's current state.
    /// </summary>
    Task<SnapshotResponse> CreateSnapshotAsync(Guid campaignId, CreateSnapshotRequest request, Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a snapshot by ID.
    /// </summary>
    Task<SnapshotResponse?> GetSnapshotAsync(Guid campaignId, Guid snapshotId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists all snapshots for a campaign with pagination.
    /// </summary>
    Task<SnapshotListResponse> ListSnapshotsAsync(Guid campaignId, ListSnapshotsRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restores a campaign to a previous snapshot state.
    /// </summary>
    Task<RestoreSnapshotResponse> RestoreSnapshotAsync(Guid campaignId, Guid snapshotId, RestoreSnapshotRequest request, Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    Task<bool> DeleteSnapshotAsync(Guid campaignId, Guid snapshotId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports a snapshot as JSON for download.
    /// </summary>
    Task<ExportSnapshotResponse?> ExportSnapshotAsync(Guid campaignId, Guid snapshotId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports a snapshot from JSON data.
    /// </summary>
    Task<SnapshotResponse> ImportSnapshotAsync(Guid campaignId, ImportSnapshotRequest request, Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compares two snapshots and returns the differences.
    /// </summary>
    Task<CompareSnapshotsResponse?> CompareSnapshotsAsync(Guid campaignId, Guid snapshotId1, Guid snapshotId2, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a snapshot's integrity.
    /// </summary>
    Task<ValidationResultResponse?> ValidateSnapshotAsync(Guid campaignId, Guid snapshotId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Archives a snapshot (hides from default list but keeps data).
    /// </summary>
    Task<bool> ArchiveSnapshotAsync(Guid campaignId, Guid snapshotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the snapshot service.
/// </summary>
public class SnapshotService : ISnapshotService
{
    private readonly CampaignDbContext _dbContext;
    private readonly ISnapshotSerializer _serializer;
    private readonly ISnapshotValidator _validator;
    private readonly ILogger<SnapshotService> _logger;
    
    public SnapshotService(
        CampaignDbContext dbContext,
        ISnapshotSerializer serializer,
        ISnapshotValidator validator,
        ILogger<SnapshotService> logger)
    {
        _dbContext = dbContext;
        _serializer = serializer;
        _validator = validator;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<SnapshotResponse> CreateSnapshotAsync(
        Guid campaignId, 
        CreateSnapshotRequest request, 
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating snapshot for campaign {CampaignId} by user {UserId}", campaignId, userId);
        
        // Verify campaign exists
        var campaign = await _dbContext.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
            ?? throw new SnapshotException($"Campaign {campaignId} not found");
        
        // Get next version number
        var nextVersion = await GetNextVersionAsync(campaignId, cancellationToken);
        
        // Build snapshot data
        var snapshotData = await BuildSnapshotDataAsync(campaign, cancellationToken);
        
        // Serialize and compute hash
        var json = _serializer.Serialize(snapshotData);
        var hash = _serializer.ComputeHash(json);
        var sizeBytes = _serializer.GetSizeBytes(json);
        
        // Create snapshot entity
        var snapshot = new CampaignSnapshot
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Version = nextVersion,
            Label = request.Label,
            Description = request.Description,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Status = SnapshotStatus.Active,
            DataJson = json,
            DataHash = hash,
            SizeBytes = sizeBytes
        };
        
        _dbContext.CampaignSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Created snapshot {SnapshotId} version {Version} for campaign {CampaignId}", 
            snapshot.Id, nextVersion, campaignId);
        
        return MapToResponse(snapshot, snapshotData);
    }
    
    /// <inheritdoc />
    public async Task<SnapshotResponse?> GetSnapshotAsync(
        Guid campaignId, 
        Guid snapshotId, 
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CampaignSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CampaignId == campaignId, cancellationToken);
        
        if (snapshot == null) return null;
        
        var (data, _) = _serializer.TryDeserialize(snapshot.DataJson);
        return MapToResponse(snapshot, data);
    }
    
    /// <inheritdoc />
    public async Task<SnapshotListResponse> ListSnapshotsAsync(
        Guid campaignId, 
        ListSnapshotsRequest request, 
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CampaignSnapshots
            .Where(s => s.CampaignId == campaignId);
        
        // Filter by status
        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }
        else
        {
            // Exclude corrupted and failed by default
            query = query.Where(s => s.Status != SnapshotStatus.Corrupted && s.Status != SnapshotStatus.Failed);
            
            if (!request.IncludeArchived)
            {
                query = query.Where(s => s.Status != SnapshotStatus.Archived);
            }
        }
        
        // Count total before pagination
        var totalCount = await query.CountAsync(cancellationToken);
        
        // Apply sorting
        query = request.SortBy switch
        {
            SnapshotSortField.Version => request.SortDescending 
                ? query.OrderByDescending(s => s.Version) 
                : query.OrderBy(s => s.Version),
            SnapshotSortField.Label => request.SortDescending 
                ? query.OrderByDescending(s => s.Label) 
                : query.OrderBy(s => s.Label),
            SnapshotSortField.SizeBytes => request.SortDescending 
                ? query.OrderByDescending(s => s.SizeBytes) 
                : query.OrderBy(s => s.SizeBytes),
            _ => request.SortDescending 
                ? query.OrderByDescending(s => s.CreatedAt) 
                : query.OrderBy(s => s.CreatedAt)
        };
        
        // Apply pagination
        var skip = (request.Page - 1) * request.PageSize;
        var snapshots = await query
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        
        var items = snapshots.Select(s =>
        {
            var (data, _) = _serializer.TryDeserialize(s.DataJson);
            return MapToResponse(s, data);
        }).ToList();
        
        return new SnapshotListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            HasNextPage = skip + snapshots.Count < totalCount,
            HasPreviousPage = request.Page > 1
        };
    }
    
    /// <inheritdoc />
    public async Task<RestoreSnapshotResponse> RestoreSnapshotAsync(
        Guid campaignId, 
        Guid snapshotId, 
        RestoreSnapshotRequest request, 
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Restoring campaign {CampaignId} from snapshot {SnapshotId}", campaignId, snapshotId);
        
        var warnings = new List<string>();
        
        // Get the snapshot
        var snapshot = await _dbContext.CampaignSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CampaignId == campaignId, cancellationToken)
            ?? throw new SnapshotException($"Snapshot {snapshotId} not found");
        
        // Verify integrity
        if (!_serializer.VerifyHash(snapshot.DataJson, snapshot.DataHash))
        {
            throw new SnapshotException("Snapshot data integrity check failed");
        }
        
        // Deserialize snapshot data
        var snapshotData = _serializer.Deserialize(snapshot.DataJson)
            ?? throw new SnapshotException("Failed to deserialize snapshot data");
        
        // Validate if requested
        if (request.ValidateBeforeRestore)
        {
            var validationResult = _validator.ValidateForRestore(snapshotData, campaignId);
            if (!validationResult.IsValid)
            {
                var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
                throw new SnapshotException($"Snapshot validation failed: {errorMessages}");
            }
            
            warnings.AddRange(validationResult.Warnings.Select(w => w.Message));
        }
        
        Guid? backupSnapshotId = null;
        
        // Use a transaction for atomicity
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Create backup if requested
            if (request.CreateBackupBeforeRestore)
            {
                var backupLabel = request.BackupLabel ?? $"Auto-backup before restore from v{snapshot.Version}";
                var backupRequest = new CreateSnapshotRequest
                {
                    Label = backupLabel,
                    Description = $"Automatic backup created before restoring from snapshot '{snapshot.Label}' (v{snapshot.Version})"
                };
                
                var backup = await CreateSnapshotAsync(campaignId, backupRequest, userId, cancellationToken);
                backupSnapshotId = backup.Id;
                
                _logger.LogInformation("Created backup snapshot {BackupId} before restore", backupSnapshotId);
            }
            
            // Restore campaign data
            var campaign = await _dbContext.Campaigns
                .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
                ?? throw new SnapshotException($"Campaign {campaignId} not found");
            
            // Update campaign properties from snapshot
            campaign.Name = snapshotData.Campaign.Name;
            campaign.Description = snapshotData.Campaign.Description;
            campaign.Status = snapshotData.Campaign.Status;
            campaign.LastPlayedAt = snapshotData.Campaign.LastPlayedAt;
            campaign.UpdatedAt = DateTime.UtcNow;
            
            // Note: Full restoration of characters, sessions, and scene state
            // would require access to those respective DbContexts/services.
            // This is a placeholder for the campaign-level restoration.
            // In a real implementation, you would inject and call the 
            // Character, GameSession, and SceneState services here.
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Successfully restored campaign {CampaignId} from snapshot {SnapshotId}", 
                campaignId, snapshotId);
            
            return new RestoreSnapshotResponse
            {
                Success = true,
                Message = $"Campaign successfully restored from snapshot v{snapshot.Version}",
                BackupSnapshotId = backupSnapshotId,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to restore campaign {CampaignId} from snapshot {SnapshotId}", 
                campaignId, snapshotId);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteSnapshotAsync(
        Guid campaignId, 
        Guid snapshotId, 
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CampaignSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CampaignId == campaignId, cancellationToken);
        
        if (snapshot == null) return false;
        
        _dbContext.CampaignSnapshots.Remove(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Deleted snapshot {SnapshotId} from campaign {CampaignId}", snapshotId, campaignId);
        
        return true;
    }
    
    /// <inheritdoc />
    public async Task<ExportSnapshotResponse?> ExportSnapshotAsync(
        Guid campaignId, 
        Guid snapshotId, 
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CampaignSnapshots
            .Include(s => s.Campaign)
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CampaignId == campaignId, cancellationToken);
        
        if (snapshot == null) return null;
        
        // Pretty print the JSON for export
        var (data, error) = _serializer.TryDeserialize(snapshot.DataJson);
        if (data == null)
        {
            _logger.LogWarning("Failed to deserialize snapshot for export: {Error}", error);
            return null;
        }
        
        // Re-serialize with indentation for readability
        var prettyJson = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        
        var fileName = $"{SanitizeFileName(snapshot.Campaign.Name)}_v{snapshot.Version}_{snapshot.CreatedAt:yyyyMMdd_HHmmss}.json";
        
        return new ExportSnapshotResponse
        {
            FileName = fileName,
            ContentType = "application/json",
            JsonData = prettyJson
        };
    }
    
    /// <inheritdoc />
    public async Task<SnapshotResponse> ImportSnapshotAsync(
        Guid campaignId, 
        ImportSnapshotRequest request, 
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing snapshot for campaign {CampaignId} by user {UserId}", campaignId, userId);
        
        // Verify campaign exists
        var campaign = await _dbContext.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken)
            ?? throw new SnapshotException($"Campaign {campaignId} not found");
        
        // Deserialize the imported data
        var (snapshotData, error) = _serializer.TryDeserialize(request.JsonData);
        if (snapshotData == null)
        {
            throw new SnapshotException($"Failed to parse imported JSON: {error}");
        }
        
        // Validate if requested
        if (request.ValidateImport)
        {
            var validationResult = _validator.ValidateImport(snapshotData);
            if (!validationResult.IsValid)
            {
                var errorMessages = string.Join("; ", validationResult.Errors.Select(e => e.Message));
                throw new SnapshotException($"Imported data validation failed: {errorMessages}");
            }
        }
        
        // Update the snapshot data to reference this campaign
        snapshotData.Campaign.Id = campaignId;
        snapshotData.CreatedAt = DateTime.UtcNow;
        
        // Re-serialize with updated data
        var json = _serializer.Serialize(snapshotData);
        var hash = _serializer.ComputeHash(json);
        var sizeBytes = _serializer.GetSizeBytes(json);
        
        // Get next version
        var nextVersion = await GetNextVersionAsync(campaignId, cancellationToken);
        
        // Create snapshot entity
        var snapshot = new CampaignSnapshot
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Version = nextVersion,
            Label = request.Label,
            Description = request.Description ?? "Imported snapshot",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Status = SnapshotStatus.Active,
            DataJson = json,
            DataHash = hash,
            SizeBytes = sizeBytes
        };
        
        _dbContext.CampaignSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Imported snapshot {SnapshotId} version {Version} for campaign {CampaignId}", 
            snapshot.Id, nextVersion, campaignId);
        
        return MapToResponse(snapshot, snapshotData);
    }
    
    /// <inheritdoc />
    public async Task<CompareSnapshotsResponse?> CompareSnapshotsAsync(
        Guid campaignId, 
        Guid snapshotId1, 
        Guid snapshotId2, 
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _dbContext.CampaignSnapshots
            .Where(s => s.CampaignId == campaignId && (s.Id == snapshotId1 || s.Id == snapshotId2))
            .ToListAsync(cancellationToken);
        
        if (snapshots.Count != 2) return null;
        
        var snapshot1 = snapshots.First(s => s.Id == snapshotId1);
        var snapshot2 = snapshots.First(s => s.Id == snapshotId2);
        
        var data1 = _serializer.Deserialize(snapshot1.DataJson);
        var data2 = _serializer.Deserialize(snapshot2.DataJson);
        
        if (data1 == null || data2 == null) return null;
        
        var differences = CompareSnapshots(data1, data2);
        
        return new CompareSnapshotsResponse
        {
            Snapshot1Id = snapshotId1,
            Snapshot2Id = snapshotId2,
            Differences = differences,
            Summary = new ComparisonSummary
            {
                AddedCount = differences.Count(d => d.Type == DifferenceType.Added),
                RemovedCount = differences.Count(d => d.Type == DifferenceType.Removed),
                ModifiedCount = differences.Count(d => d.Type == DifferenceType.Modified),
                TotalDifferences = differences.Count
            }
        };
    }
    
    /// <inheritdoc />
    public async Task<ValidationResultResponse?> ValidateSnapshotAsync(
        Guid campaignId, 
        Guid snapshotId, 
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CampaignSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CampaignId == campaignId, cancellationToken);
        
        if (snapshot == null) return null;
        
        // Verify data integrity
        var hashValid = _serializer.VerifyHash(snapshot.DataJson, snapshot.DataHash);
        if (!hashValid)
        {
            // Mark as corrupted
            snapshot.Status = SnapshotStatus.Corrupted;
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            return new ValidationResultResponse
            {
                IsValid = false,
                Errors = [new ValidationErrorResponse 
                { 
                    Code = "DATA_CORRUPTED", 
                    Message = "Snapshot data integrity check failed" 
                }]
            };
        }
        
        // Deserialize and validate content
        var (data, parseError) = _serializer.TryDeserialize(snapshot.DataJson);
        if (data == null)
        {
            snapshot.Status = SnapshotStatus.Corrupted;
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            return new ValidationResultResponse
            {
                IsValid = false,
                Errors = [new ValidationErrorResponse 
                { 
                    Code = "PARSE_ERROR", 
                    Message = $"Failed to parse snapshot data: {parseError}" 
                }]
            };
        }
        
        var validationResult = _validator.Validate(data);
        
        return new ValidationResultResponse
        {
            IsValid = validationResult.IsValid,
            Errors = validationResult.Errors.Select(e => new ValidationErrorResponse
            {
                Code = e.Code,
                Message = e.Message,
                Field = e.Field
            }).ToList(),
            Warnings = validationResult.Warnings.Select(w => new ValidationWarningResponse
            {
                Code = w.Code,
                Message = w.Message,
                Field = w.Field
            }).ToList()
        };
    }
    
    /// <inheritdoc />
    public async Task<bool> ArchiveSnapshotAsync(
        Guid campaignId, 
        Guid snapshotId, 
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _dbContext.CampaignSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.CampaignId == campaignId, cancellationToken);
        
        if (snapshot == null) return false;
        
        snapshot.Status = SnapshotStatus.Archived;
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Archived snapshot {SnapshotId} from campaign {CampaignId}", snapshotId, campaignId);
        
        return true;
    }
    
    #region Private Methods
    
    private async Task<int> GetNextVersionAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var maxVersion = await _dbContext.CampaignSnapshots
            .Where(s => s.CampaignId == campaignId)
            .MaxAsync(s => (int?)s.Version, cancellationToken) ?? 0;
        
        return maxVersion + 1;
    }
    
    private async Task<SnapshotData> BuildSnapshotDataAsync(DataAccess.Models.Campaign campaign, CancellationToken cancellationToken)
    {
        // In a real implementation, this would gather data from multiple sources:
        // - Character service
        // - Game session service  
        // - Scene state service
        // For now, we create the basic structure with campaign data
        
        return await Task.FromResult(new SnapshotData
        {
            SchemaVersion = 1,
            CreatedAt = DateTime.UtcNow,
            Campaign = new CampaignData
            {
                Id = campaign.Id,
                Name = campaign.Name,
                Description = campaign.Description,
                DungeonMasterId = campaign.DungeonMasterId,
                CreatedAt = campaign.CreatedAt,
                LastPlayedAt = campaign.LastPlayedAt,
                Status = campaign.Status
            },
            Characters = [], // Would be populated from Character service
            Sessions = [],   // Would be populated from GameSession service
            SceneState = new SceneStateData(), // Would be populated from Scene service
            Settings = new GameSettingsData()
        });
    }
    
    private static SnapshotResponse MapToResponse(CampaignSnapshot snapshot, SnapshotData? data)
    {
        return new SnapshotResponse
        {
            Id = snapshot.Id,
            CampaignId = snapshot.CampaignId,
            Version = snapshot.Version,
            Label = snapshot.Label,
            Description = snapshot.Description,
            CreatedBy = snapshot.CreatedBy,
            CreatedAt = snapshot.CreatedAt,
            Status = snapshot.Status,
            SizeBytes = snapshot.SizeBytes,
            SizeFormatted = FormatBytes(snapshot.SizeBytes),
            ContentSummary = data != null ? new SnapshotContentSummary
            {
                CampaignName = data.Campaign.Name,
                CharacterCount = data.Characters.Count,
                SessionCount = data.Sessions.Count,
                HasSceneState = data.SceneState.CurrentMapId.HasValue,
                LastSessionDate = data.Sessions.LastOrDefault()?.EndedAt ?? data.Sessions.LastOrDefault()?.StartedAt
            } : null
        };
    }
    
    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
    
    private static List<SnapshotDifference> CompareSnapshots(SnapshotData data1, SnapshotData data2)
    {
        var differences = new List<SnapshotDifference>();
        
        // Compare campaign metadata
        if (data1.Campaign.Name != data2.Campaign.Name)
        {
            differences.Add(new SnapshotDifference
            {
                Category = "Campaign",
                Field = "Name",
                Type = DifferenceType.Modified,
                OldValue = data1.Campaign.Name,
                NewValue = data2.Campaign.Name
            });
        }
        
        if (data1.Campaign.Description != data2.Campaign.Description)
        {
            differences.Add(new SnapshotDifference
            {
                Category = "Campaign",
                Field = "Description",
                Type = DifferenceType.Modified,
                OldValue = data1.Campaign.Description,
                NewValue = data2.Campaign.Description
            });
        }
        
        // Compare character counts
        var chars1 = data1.Characters.Select(c => c.Id).ToHashSet();
        var chars2 = data2.Characters.Select(c => c.Id).ToHashSet();
        
        foreach (var added in chars2.Except(chars1))
        {
            var char2 = data2.Characters.First(c => c.Id == added);
            differences.Add(new SnapshotDifference
            {
                Category = "Characters",
                Field = char2.Name,
                Type = DifferenceType.Added,
                NewValue = $"Level {char2.Level} {char2.Class}"
            });
        }
        
        foreach (var removed in chars1.Except(chars2))
        {
            var char1 = data1.Characters.First(c => c.Id == removed);
            differences.Add(new SnapshotDifference
            {
                Category = "Characters",
                Field = char1.Name,
                Type = DifferenceType.Removed,
                OldValue = $"Level {char1.Level} {char1.Class}"
            });
        }
        
        // Compare session counts
        if (data1.Sessions.Count != data2.Sessions.Count)
        {
            differences.Add(new SnapshotDifference
            {
                Category = "Sessions",
                Field = "Count",
                Type = DifferenceType.Modified,
                OldValue = data1.Sessions.Count.ToString(),
                NewValue = data2.Sessions.Count.ToString()
            });
        }
        
        // Compare scene state
        if (data1.SceneState.CurrentMapId != data2.SceneState.CurrentMapId)
        {
            differences.Add(new SnapshotDifference
            {
                Category = "SceneState",
                Field = "CurrentMap",
                Type = DifferenceType.Modified,
                OldValue = data1.SceneState.CurrentMapId?.ToString(),
                NewValue = data2.SceneState.CurrentMapId?.ToString()
            });
        }
        
        return differences;
    }
    
    #endregion
}

/// <summary>
/// Exception thrown by snapshot operations.
/// </summary>
public class SnapshotException : Exception
{
    public SnapshotException(string message) : base(message)
    {
    }
    
    public SnapshotException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

