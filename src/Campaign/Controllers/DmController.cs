using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.Controllers;

/// <summary>
/// DM-only actions for a campaign session (grant items, etc.).
/// </summary>
[ApiController]
[Route("api/campaigns/{campaignId:guid}/dm")]
[Produces("application/json")]
public class DmController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly IUserContextService _userContextService;
    private readonly ILogger<DmController> _logger;

    public DmController(
        ICampaignService campaignService,
        IUserContextService userContextService,
        ILogger<DmController> logger)
    {
        _campaignService = campaignService;
        _userContextService = userContextService;
        _logger = logger;
    }

    /// <summary>
    /// Grants an item from the DM to a player character.
    /// Only the Dungeon Master of the campaign can call this.
    /// </summary>
    [HttpPost("grant-item")]
    [ProducesResponseType(typeof(DmGrantItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantItem(
        Guid campaignId,
        [FromBody] DmGrantItemRequest request,
        CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();

        // Verify caller is the DM of this campaign
        var campaign = await _campaignService.GetCampaignAsync(campaignId, userId, ct);
        if (campaign == null)
            return NotFound(new ProblemDetails { Title = "Campaign not found" });

        if (!campaign.IsDungeonMaster)
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Forbidden", Detail = "Only the Dungeon Master can grant items." });

        var result = new DmGrantItemResponse
        {
            TargetUserId = request.TargetUserId,
            TargetUserName = request.TargetUserName ?? "Joueur",
            ItemId = request.ItemId,
            ItemName = request.ItemName,
            Quantity = request.Quantity,
            Description = request.Description,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation(
            "DM {UserId} granted {Quantity}x {ItemName} to {TargetUserId} in campaign {CampaignId}",
            userId, request.Quantity, request.ItemName, request.TargetUserId, campaignId);

        return Ok(result);
    }
}

/// <summary>
/// Request body for granting an item.
/// </summary>
public class DmGrantItemRequest
{
    public Guid TargetUserId { get; set; }
    public string? TargetUserName { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
}

/// <summary>
/// Response for grant-item.
/// </summary>
public class DmGrantItemResponse
{
    public Guid TargetUserId { get; set; }
    public string TargetUserName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}
