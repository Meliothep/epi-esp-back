using DnDiscord.Campaign.BL.Campaigns;
using DnDiscord.Campaign.BL.Campaigns.DTOs;
using DnDiscord.Campaign.DataAccess.Models;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.Controllers;

/// <summary>
/// API controller for campaign management.
/// </summary>
[ApiController]
[Route("api/campaigns")]
[Produces("application/json")]
public class CampaignController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly IUserContextService _userContextService;
    private readonly ILogger<CampaignController> _logger;

    public CampaignController(
        ICampaignService campaignService,
        IUserContextService userContextService,
        ILogger<CampaignController> logger)
    {
        _campaignService = campaignService;
        _userContextService = userContextService;
        _logger = logger;
    }
    
    #region Campaign CRUD
    
    /// <summary>
    /// Creates a new campaign.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CampaignDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCampaign(
        [FromBody] CreateCampaignRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var campaign = await _campaignService.CreateCampaignAsync(request, userId, ct);
            
            return CreatedAtAction(
                nameof(GetCampaign),
                new { id = campaign.Id },
                campaign);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Campaign Creation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Gets a list of campaigns.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CampaignListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCampaigns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] CampaignStatus? status = null,
        [FromQuery] bool? isPublic = null,
        [FromQuery] CampaignRoleFilter? roleFilter = null,
        [FromQuery] CampaignSortField sortBy = CampaignSortField.CreatedAt,
        [FromQuery] bool sortDescending = true,
        CancellationToken ct = default)
    {
        var userId = _userContextService.GetCurrentUserId();
        var filter = new CampaignFilterRequest
        {
            Page = page,
            PageSize = Math.Clamp(pageSize, 1, 100),
            Search = search,
            Status = status,
            IsPublic = isPublic,
            RoleFilter = roleFilter,
            SortBy = sortBy,
            SortDescending = sortDescending
        };
        
        var result = await _campaignService.ListCampaignsAsync(filter, userId, ct);
        return Ok(result);
    }
    
    /// <summary>
    /// Gets a campaign by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CampaignDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCampaign(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();
        var campaign = await _campaignService.GetCampaignAsync(id, userId, ct);
        
        if (campaign == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Campaign Not Found",
                Detail = $"Campaign {id} not found or you don't have access",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return Ok(campaign);
    }
    
    /// <summary>
    /// Updates a campaign.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CampaignDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCampaign(
        [FromRoute] Guid id,
        [FromBody] UpdateCampaignRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var campaign = await _campaignService.UpdateCampaignAsync(id, request, userId, ct);
            
            if (campaign == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Campaign Not Found",
                    Detail = $"Campaign {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return Ok(campaign);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Update Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Deletes a campaign.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCampaign(
        [FromRoute] Guid id,
        [FromQuery] bool hardDelete = false,
        CancellationToken ct = default)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var deleted = await _campaignService.DeleteCampaignAsync(id, userId, hardDelete, ct);
            
            if (!deleted)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Campaign Not Found",
                    Detail = $"Campaign {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return NoContent();
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Delete Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    #endregion
    
    #region Invite Codes
    
    /// <summary>
    /// Generates a new invite code for a campaign.
    /// </summary>
    [HttpPost("{id:guid}/invite")]
    [ProducesResponseType(typeof(InviteCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateInviteCode(
        [FromRoute] Guid id,
        [FromBody] GenerateInviteCodeRequest? request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            request ??= new GenerateInviteCodeRequest();
            
            var result = await _campaignService.GenerateInviteCodeAsync(id, request, userId, ct);
            
            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Campaign Not Found",
                    Detail = $"Campaign {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return Ok(result);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Generate Invite Code Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Joins a campaign using an invite code.
    /// </summary>
    [HttpPost("join")]
    [ProducesResponseType(typeof(CampaignDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinCampaign(
        [FromBody] JoinCampaignRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var campaign = await _campaignService.JoinCampaignAsync(request, userId, ct);
            
            return Ok(campaign);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Join Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Joins a public campaign directly by its ID (no invite code required).
    /// </summary>
    [HttpPost("{id:guid}/join-public")]
    [ProducesResponseType(typeof(CampaignDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinPublicCampaign(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            var userId   = _userContextService.GetCurrentUserId();
            var campaign = await _campaignService.JoinPublicCampaignAsync(id, userId, ct);

            if (campaign == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title  = "Campaign Not Found",
                    Detail = $"Campaign {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(campaign);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Join Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while joining public campaign {CampaignId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title  = "Internal Server Error",
                Detail = "Une erreur inattendue s'est produite.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    #endregion

    #region Campaign Tree

    /// <summary>
    /// Updates the campaign canvas tree definition (nodes + connections).
    /// </summary>
    [HttpPut("{id:guid}/manager")]
    [ProducesResponseType(typeof(CampaignDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCampaignTree(
        [FromRoute] Guid id,
        [FromBody] UpdateCampaignManagerRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var campaign = await _campaignService.UpdateCampaignTreeAsync(id, request.CampaignTreeDefinition, userId, ct);

            if (campaign == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Campaign Not Found",
                    Detail = $"Campaign {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(campaign);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Update Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    #endregion

    #region Members
    
    /// <summary>
    /// Gets the list of members in a campaign.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(CampaignMemberListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();
        var result = await _campaignService.GetMembersAsync(id, userId, ct);
        return Ok(result);
    }
    
    /// <summary>
    /// Adds a member to a campaign.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(CampaignMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(
        [FromRoute] Guid id,
        [FromBody] AddMemberRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var member = await _campaignService.AddMemberAsync(id, request, userId, ct);
            
            if (member == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Campaign Not Found",
                    Detail = $"Campaign {id} not found",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return CreatedAtAction(nameof(GetMembers), new { id }, member);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Add Member Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Updates a campaign member.
    /// </summary>
    [HttpPut("{id:guid}/members/{memberId:guid}")]
    [ProducesResponseType(typeof(CampaignMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember(
        [FromRoute] Guid id,
        [FromRoute] Guid memberId,
        [FromBody] UpdateMemberRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var member = await _campaignService.UpdateMemberAsync(id, memberId, request, userId, ct);
            
            if (member == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Member Not Found",
                    Detail = $"Member {memberId} not found in campaign {id}",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return Ok(member);
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Update Member Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Removes a member from a campaign.
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        [FromRoute] Guid id,
        [FromRoute] Guid memberId,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var removed = await _campaignService.RemoveMemberAsync(id, memberId, userId, ct);
            
            if (!removed)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Member Not Found",
                    Detail = $"Member {memberId} not found in campaign {id}",
                    Status = StatusCodes.Status404NotFound
                });
            }
            
            return NoContent();
        }
        catch (CampaignException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Remove Member Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    
    /// <summary>
    /// Leaves a campaign (for the current user).
    /// </summary>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveCampaign(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();
        var left = await _campaignService.LeaveCampaignAsync(id, userId, ct);
        
        if (!left)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not a Member",
                Detail = "You are not a member of this campaign",
                Status = StatusCodes.Status404NotFound
            });
        }
        
        return NoContent();
    }
    
    #endregion
}

