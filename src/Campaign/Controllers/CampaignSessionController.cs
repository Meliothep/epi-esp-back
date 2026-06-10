using DnDiscord.Campaign.BL.Campaigns.DTOs;
using DnDiscord.Campaign.BL.Sessions;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.Controllers;

[ApiController]
[Route("api/campaigns/{campaignId:guid}/sessions")]
[Produces("application/json")]
[Authorize]
public class CampaignSessionController : ControllerBase
{
    private readonly ICampaignSessionService _sessionService;
    private readonly IUserContextService _userContextService;
    private readonly ILogger<CampaignSessionController> _logger;
    private readonly ICampaignRealtimeNotifier _realtime;

    public CampaignSessionController(
        ICampaignSessionService sessionService,
        IUserContextService userContextService,
        ILogger<CampaignSessionController> logger,
        ICampaignRealtimeNotifier realtime)
    {
        _sessionService = sessionService;
        _userContextService = userContextService;
        _logger = logger;
        _realtime = realtime;
    }

    /// <summary>Create a new game session for a campaign.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GameSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession(
        Guid campaignId,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var session = await _sessionService.CreateSessionAsync(campaignId, userId, ct);
            return CreatedAtAction(nameof(GetSession),
                new { campaignId, sessionId = session.Id }, session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot create session", Detail = ex.Message });
        }
    }

    /// <summary>List all sessions for a campaign.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GameSessionListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSessions(Guid campaignId, CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();
        var result = await _sessionService.ListSessionsAsync(campaignId, userId, ct);
        return Ok(result);
    }

    /// <summary>Get a specific session with its history.</summary>
    [HttpGet("{sessionId:guid}")]
    [ProducesResponseType(typeof(GameSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(Guid campaignId, Guid sessionId, CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();
        var session = await _sessionService.GetSessionAsync(sessionId, userId, ct);
        return session is null ? NotFound() : Ok(session);
    }

    /// <summary>Record navigation to a new node (saves history entry + updates CurrentNodeId).</summary>
    [HttpPost("{sessionId:guid}/advance")]
    [ProducesResponseType(typeof(GameSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdvanceSession(
        Guid campaignId,
        Guid sessionId,
        [FromBody] AdvanceSessionRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = _userContextService.GetCurrentUserId();
            var session = await _sessionService.AdvanceSessionAsync(sessionId, request, userId, ct);
            return session is null ? NotFound() : Ok(session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Cannot advance session", Detail = ex.Message });
        }
    }

    /// <summary>Mark a session as completed.</summary>
    [HttpPost("{sessionId:guid}/complete")]
    [ProducesResponseType(typeof(GameSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSession(Guid campaignId, Guid sessionId, CancellationToken ct)
    {
        var userId = _userContextService.GetCurrentUserId();
        var session = await _sessionService.CompleteSessionAsync(sessionId, userId, ct);
        if (session is null) return NotFound();

        // Notify live clients subscribed to this campaign (session page + lobby)
        // so they can exit without requiring a manual refresh.
        try
        {
            await _realtime.NotifySessionCompletedAsync(campaignId, sessionId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to broadcast CampaignSessionCompleted for campaign {CampaignId} session {SessionId}",
                campaignId, sessionId);
        }

        return Ok(session);
    }
}
