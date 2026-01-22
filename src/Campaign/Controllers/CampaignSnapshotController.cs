using DnDiscord.Campaign.BL.Snapshots;
using DnDiscord.Campaign.BL.Snapshots.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DnDiscord.Campaign.Controllers;

/// <summary>
/// API controller for campaign snapshot management (backup/restore functionality).
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/snapshots")]
[Produces("application/json")]
public class CampaignSnapshotController : ControllerBase
{
    private readonly ISnapshotService _snapshotService;
    private readonly ILogger<CampaignSnapshotController> _logger;
    
    public CampaignSnapshotController(
        ISnapshotService snapshotService,
        ILogger<CampaignSnapshotController> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }
    
    /// <summary>
    /// Creates a new snapshot of the campaign's current state.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="request">Snapshot creation request.</param>
    /// <returns>The created snapshot details.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(SnapshotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSnapshot(
        [FromRoute] Guid campaignId,
        [FromBody] CreateSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Get actual user ID from authentication context
            var userId = GetCurrentUserId();
            
            var snapshot = await _snapshotService.CreateSnapshotAsync(campaignId, request, userId, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetSnapshot),
                new { campaignId, id = snapshot.Id },
                snapshot);
        }
        catch (SnapshotException ex)
        {
            _logger.LogWarning(ex, "Failed to create snapshot for campaign {CampaignId}", campaignId);
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Creation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }
    
    /// <summary>
    /// Gets a list of snapshots for a campaign.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="includeArchived">Include archived snapshots.</param>
    /// <param name="sortBy">Field to sort by.</param>
    /// <param name="sortDescending">Sort in descending order.</param>
    /// <returns>Paginated list of snapshots.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(SnapshotListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSnapshots(
        [FromRoute] Guid campaignId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeArchived = false,
        [FromQuery] SnapshotSortField sortBy = SnapshotSortField.CreatedAt,
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ListSnapshotsRequest
        {
            Page = page,
            PageSize = Math.Clamp(pageSize, 1, 100),
            IncludeArchived = includeArchived,
            SortBy = sortBy,
            SortDescending = sortDescending
        };
        
        var result = await _snapshotService.ListSnapshotsAsync(campaignId, request, cancellationToken);
        
        return Ok(result);
    }
    
    /// <summary>
    /// Gets details of a specific snapshot.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID.</param>
    /// <returns>Snapshot details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var snapshot = await _snapshotService.GetSnapshotAsync(campaignId, id, cancellationToken);
        
        if (snapshot == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Not Found",
                Detail = $"Snapshot {id} not found for campaign {campaignId}",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return Ok(snapshot);
    }
    
    /// <summary>
    /// Restores a campaign to a previous snapshot state.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID to restore.</param>
    /// <param name="request">Restore options.</param>
    /// <returns>Restore operation result.</returns>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(typeof(RestoreSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreSnapshot(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        [FromBody] RestoreSnapshotRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            request ??= new RestoreSnapshotRequest();
            
            var result = await _snapshotService.RestoreSnapshotAsync(campaignId, id, request, userId, cancellationToken);
            
            return Ok(result);
        }
        catch (SnapshotException ex)
        {
            _logger.LogWarning(ex, "Failed to restore snapshot {SnapshotId} for campaign {CampaignId}", id, campaignId);
            return BadRequest(new ProblemDetails
            {
                Title = "Restore Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID to delete.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSnapshot(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _snapshotService.DeleteSnapshotAsync(campaignId, id, cancellationToken);
        
        if (!deleted)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Not Found",
                Detail = $"Snapshot {id} not found for campaign {campaignId}",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return NoContent();
    }
    
    /// <summary>
    /// Exports a snapshot as a JSON file for download.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID to export.</param>
    /// <returns>JSON file download.</returns>
    [HttpGet("{id:guid}/export")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportSnapshot(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var export = await _snapshotService.ExportSnapshotAsync(campaignId, id, cancellationToken);
        
        if (export == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Not Found",
                Detail = $"Snapshot {id} not found for campaign {campaignId}",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        var bytes = System.Text.Encoding.UTF8.GetBytes(export.JsonData);
        return File(bytes, export.ContentType, export.FileName);
    }
    
    /// <summary>
    /// Imports a snapshot from JSON data.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="request">Import request containing JSON data.</param>
    /// <returns>The imported snapshot details.</returns>
    [HttpPost("import")]
    [ProducesResponseType(typeof(SnapshotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportSnapshot(
        [FromRoute] Guid campaignId,
        [FromBody] ImportSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            var snapshot = await _snapshotService.ImportSnapshotAsync(campaignId, request, userId, cancellationToken);
            
            return CreatedAtAction(
                nameof(GetSnapshot),
                new { campaignId, id = snapshot.Id },
                snapshot);
        }
        catch (SnapshotException ex)
        {
            _logger.LogWarning(ex, "Failed to import snapshot for campaign {CampaignId}", campaignId);
            return BadRequest(new ProblemDetails
            {
                Title = "Import Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Compares two snapshots and returns the differences.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id1">First snapshot ID.</param>
    /// <param name="id2">Second snapshot ID.</param>
    /// <returns>Comparison results.</returns>
    [HttpGet("compare")]
    [ProducesResponseType(typeof(CompareSnapshotsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompareSnapshots(
        [FromRoute] Guid campaignId,
        [FromQuery] Guid id1,
        [FromQuery] Guid id2,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.CompareSnapshotsAsync(campaignId, id1, id2, cancellationToken);
        
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshots Not Found",
                Detail = "One or both snapshots not found",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Validates a snapshot's integrity and data.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID to validate.</param>
    /// <returns>Validation results.</returns>
    [HttpGet("{id:guid}/validate")]
    [ProducesResponseType(typeof(ValidationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateSnapshot(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _snapshotService.ValidateSnapshotAsync(campaignId, id, cancellationToken);
        
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Not Found",
                Detail = $"Snapshot {id} not found for campaign {campaignId}",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return Ok(result);
    }
    
    /// <summary>
    /// Archives a snapshot (hides from default list but keeps data).
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID to archive.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveSnapshot(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var archived = await _snapshotService.ArchiveSnapshotAsync(campaignId, id, cancellationToken);
        
        if (!archived)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Not Found",
                Detail = $"Snapshot {id} not found for campaign {campaignId}",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return NoContent();
    }
    
    /// <summary>
    /// Recalculates the hash for a snapshot based on the PostgreSQL-normalized JSON.
    /// Use this to repair snapshots that have invalid hashes due to jsonb normalization.
    /// </summary>
    /// <param name="campaignId">The campaign ID.</param>
    /// <param name="id">The snapshot ID to repair.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("{id:guid}/recalculate-hash")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecalculateHash(
        [FromRoute] Guid campaignId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var success = await _snapshotService.RecalculateHashAsync(campaignId, id, cancellationToken);
        
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot Not Found",
                Detail = $"Snapshot {id} not found for campaign {campaignId}",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return NoContent();
    }
    
    /// <summary>
    /// Gets the current user ID from the authentication context.
    /// Converts the Discord ID (string) from JWT to a deterministic Guid.
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var discordId = User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(discordId))
        {
            _logger.LogError("Unable to extract user ID from JWT claims");
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        // Convert Discord ID string to deterministic Guid using MD5 hash
        return ConvertDiscordIdToGuid(discordId);
    }

    /// <summary>
    /// Converts a Discord ID (string) to a deterministic Guid using MD5 hashing.
    /// </summary>
    private static Guid ConvertDiscordIdToGuid(string discordId)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(discordId));
        return new Guid(hash);
    }
}

