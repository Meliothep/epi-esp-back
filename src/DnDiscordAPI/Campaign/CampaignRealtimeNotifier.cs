using System;
using System.Threading;
using System.Threading.Tasks;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Multiplayer.Hubs;
using Multiplayer.Services;

namespace DnDiscordAPI.Campaign;

public sealed class CampaignRealtimeNotifier : ICampaignRealtimeNotifier
{
    private readonly IHubContext<GameHub> _gameHub;
    private readonly SessionManager _sessionManager;
    private readonly StateManager _stateManager;
    private readonly ILogger<CampaignRealtimeNotifier> _logger;

    public CampaignRealtimeNotifier(
        IHubContext<GameHub> gameHub,
        SessionManager sessionManager,
        StateManager stateManager,
        ILogger<CampaignRealtimeNotifier> logger)
    {
        _gameHub = gameHub;
        _sessionManager = sessionManager;
        _stateManager = stateManager;
        _logger = logger;
    }

    public async Task NotifySessionCompletedAsync(Guid campaignId, Guid sessionId, CancellationToken ct = default)
    {
        // Tear down any live (in-memory) session of this campaign. Completing the
        // DB session used to leave the hub session alive — and since the DM's
        // websocket stays connected while browsing the SPA, the background cleanup
        // never evicted it, so GetActiveCampaignSession kept returning a ghost
        // session and CampaignView showed a stale "session in progress" banner.
        foreach (var live in _sessionManager.GetSessionsByCampaign(campaignId))
        {
            try
            {
                await _gameHub.Clients.Group(live.SessionId).SendAsync("SessionEnded", new
                {
                    sessionId = live.SessionId,
                    reason = "Campaign session completed",
                    timestamp = DateTime.UtcNow,
                }, ct);
            }
            catch (Exception ex)
            {
                // Notification only — the teardown below must still run.
                _logger.LogWarning(ex,
                    "Failed to broadcast SessionEnded for live session {SessionId} (campaign {CampaignId})",
                    live.SessionId, campaignId);
            }

            _sessionManager.RemoveSession(live.SessionId);
            _stateManager.ClearSnapshot(live.SessionId);
            _logger.LogInformation(
                "Removed live session {SessionId} after campaign session {DbSessionId} completed (campaign {CampaignId})",
                live.SessionId, sessionId, campaignId);
        }

        await _gameHub.Clients.Group($"campaign_{campaignId:N}").SendAsync(
            "CampaignSessionCompleted",
            new
            {
                campaignId,
                sessionId,
                timestamp = DateTime.UtcNow,
            },
            ct);
    }
}
