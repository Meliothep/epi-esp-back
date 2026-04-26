using DnDiscord.Campaign.BL.Maps;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DnDiscord.Campaign.Controllers;

/// <summary>
/// CRUD for standalone (non-campaign) maps owned by the current user.
/// Reads and writes are restricted to the owner — no campaign membership required.
/// </summary>
[ApiController]
[Authorize]
[Route("api/maps/mine")]
[Produces("application/json")]
public class UserMapsController : ControllerBase
{
    private readonly IUserMapService _userMapService;
    private readonly IUserContextService _userContext;

    public UserMapsController(IUserMapService userMapService, IUserContextService userContext)
    {
        _userMapService = userMapService;
        _userContext = userContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CampaignMapDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var ownerId = _userContext.GetCurrentUserId();
        var maps = await _userMapService.ListByOwnerAsync(ownerId, ct);
        return Ok(maps);
    }

    [HttpGet("{mapId:guid}")]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid mapId, CancellationToken ct)
    {
        var ownerId = _userContext.GetCurrentUserId();
        var map = await _userMapService.GetByOwnerAsync(ownerId, mapId, ct);
        if (map is null) return NotFound(new ProblemDetails { Title = "Map not found" });
        return Ok(map);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUserMapRequest request, CancellationToken ct)
    {
        var ownerId = _userContext.GetCurrentUserId();
        var created = await _userMapService.CreateAsync(ownerId, request, ct);
        return CreatedAtAction(nameof(Get), new { mapId = created.Id }, created);
    }

    [HttpPut("{mapId:guid}")]
    [ProducesResponseType(typeof(CampaignMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid mapId, [FromBody] UpdateUserMapRequest request, CancellationToken ct)
    {
        var ownerId = _userContext.GetCurrentUserId();
        var updated = await _userMapService.UpdateAsync(ownerId, mapId, request, ct);
        if (updated is null) return NotFound(new ProblemDetails { Title = "Map not found" });
        return Ok(updated);
    }

    [HttpDelete("{mapId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid mapId, CancellationToken ct)
    {
        var ownerId = _userContext.GetCurrentUserId();
        var removed = await _userMapService.DeleteAsync(ownerId, mapId, ct);
        if (!removed) return NotFound(new ProblemDetails { Title = "Map not found" });
        return NoContent();
    }
}
