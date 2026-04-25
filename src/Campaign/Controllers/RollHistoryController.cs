using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.BL.Rolls;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DnDiscord.Campaign.Controllers;

/// <summary>
/// Read-only journal of dice rolls scoped to a campaign.
/// Accessible to any campaign member (DM or players).
/// </summary>
[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId:guid}/rolls")]
[Produces("application/json")]
public class RollHistoryController : ControllerBase
{
    private readonly CampaignDbContext _db;
    private readonly ICampaignService _campaignService;
    private readonly IUserContextService _userContext;

    public RollHistoryController(
        CampaignDbContext db,
        ICampaignService campaignService,
        IUserContextService userContext)
    {
        _db = db;
        _campaignService = campaignService;
        _userContext = userContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RollHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        Guid campaignId,
        [FromQuery] int limit = 50,
        [FromQuery] Guid? userId = null,
        CancellationToken ct = default)
    {
        var currentUserId = _userContext.GetCurrentUserId();

        // Reuse the same visibility check as CampaignMapsController:
        // GetCampaignAsync returns null when the campaign doesn't exist OR the
        // caller is neither a member nor the DM, so a null result means 404.
        var campaign = await _campaignService.GetCampaignAsync(campaignId, currentUserId, ct);
        if (campaign is null)
            return NotFound(new ProblemDetails { Title = "Campaign not found" });

        if (limit < 1) limit = 1;
        if (limit > 200) limit = 200;

        var query = _db.RollHistory.AsNoTracking()
            .Where(r => r.CampaignId == campaignId);

        if (userId is Guid filterUserId)
            query = query.Where(r => r.UserId == filterUserId);

        var rolls = await query
            .OrderByDescending(r => r.RolledAt)
            .Take(limit)
            .Select(r => new RollHistoryDto
            {
                Id = r.Id,
                RequestId = r.RequestId,
                UserId = r.UserId,
                UserName = r.UserName,
                DiceType = r.DiceType,
                Value = r.Value,
                Label = r.Label,
                RolledAt = r.RolledAt,
            })
            .ToListAsync(ct);

        return Ok(rolls);
    }
}
