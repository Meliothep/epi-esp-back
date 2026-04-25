using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.BL.Maps;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscord.Campaign.Controllers;

/// <summary>
/// CRUD for maps scoped to a campaign. Reads allowed to any member of the campaign,
/// writes restricted to the DM.
/// </summary>
[ApiController]
[Authorize]
[Route("api/campaigns/{campaignId:guid}/maps")]
[Produces("application/json")]
public class CampaignMapsController : ControllerBase
{
    private readonly ICampaignMapService _mapService;
    private readonly ICampaignService _campaignService;
    private readonly IUserContextService _userContext;

    public CampaignMapsController(
        ICampaignMapService mapService,
        ICampaignService campaignService,
        IUserContextService userContext)
    {
        _mapService = mapService;
        _campaignService = campaignService;
        _userContext = userContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CampaignMapDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid campaignId, CancellationToken ct)
    {
        if (!await IsCampaignVisibleAsync(campaignId, ct))
            return NotFound(new ProblemDetails { Title = "Campaign not found" });

        var maps = await _mapService.ListAsync(campaignId, ct);
        return Ok(maps);
    }

    [HttpGet("{mapId:guid}")]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid campaignId, Guid mapId, CancellationToken ct)
    {
        if (!await IsCampaignVisibleAsync(campaignId, ct))
            return NotFound(new ProblemDetails { Title = "Campaign not found" });

        var map = await _mapService.GetAsync(campaignId, mapId, ct);
        if (map is null) return NotFound(new ProblemDetails { Title = "Map not found" });
        return Ok(map);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid campaignId, [FromBody] CreateCampaignMapRequest request, CancellationToken ct)
    {
        if (!await _campaignService.IsDungeonMasterAsync(campaignId, _userContext.GetCurrentUserId(), ct))
            return Forbid();

        var created = await _mapService.CreateAsync(campaignId, _userContext.GetCurrentUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { campaignId, mapId = created.Id }, created);
    }

    [HttpPut("{mapId:guid}")]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid campaignId, Guid mapId, [FromBody] UpdateCampaignMapRequest request, CancellationToken ct)
    {
        if (!await _campaignService.IsDungeonMasterAsync(campaignId, _userContext.GetCurrentUserId(), ct))
            return Forbid();

        var updated = await _mapService.UpdateAsync(campaignId, mapId, request, ct);
        if (updated is null) return NotFound(new ProblemDetails { Title = "Map not found" });
        return Ok(updated);
    }

    [HttpDelete("{mapId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid campaignId, Guid mapId, CancellationToken ct)
    {
        if (!await _campaignService.IsDungeonMasterAsync(campaignId, _userContext.GetCurrentUserId(), ct))
            return Forbid();

        var removed = await _mapService.DeleteAsync(campaignId, mapId, ct);
        if (!removed) return NotFound(new ProblemDetails { Title = "Map not found" });
        return NoContent();
    }

    /// <summary>
    /// Récupère une map par son ID dans le contexte d'une campagne, sans restriction
    /// d'owner. Accessible à tous les membres visibles de la campagne.
    /// Utilisé par les joueurs en session pour mettre en cache une map dont ils
    /// ne sont pas owners (ex : map créée par le MJ).
    /// Route : GET api/campaigns/{campaignId}/maps/session/{mapId}
    /// </summary>
    [HttpGet("session/{mapId:guid}")]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForSession(Guid campaignId, Guid mapId, CancellationToken ct)
    {
        if (!await IsCampaignVisibleAsync(campaignId, ct))
            return NotFound(new ProblemDetails { Title = "Campaign not found" });

        var map = await _mapService.GetByMapIdAsync(mapId, ct);
        if (map is null) return NotFound(new ProblemDetails { Title = "Map not found" });
        return Ok(map);
    }

    /// <summary>
    /// A campaign is visible to any authenticated user for this POC — public /
    /// member-only gating lives in <see cref="ICampaignService.GetCampaignAsync"/>.
    /// We reuse that guard so the maps endpoint matches the campaign endpoint's
    /// visibility rules exactly.
    /// </summary>
    private async Task<bool> IsCampaignVisibleAsync(Guid campaignId, CancellationToken ct)
    {
        var campaign = await _campaignService.GetCampaignAsync(campaignId, _userContext.GetCurrentUserId(), ct);
        return campaign is not null;
    }
}
