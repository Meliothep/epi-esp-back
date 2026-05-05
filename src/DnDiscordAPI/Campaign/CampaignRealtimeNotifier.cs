using System;
using System.Threading;
using System.Threading.Tasks;
using DnDiscord.Campaign.Services;
using Microsoft.AspNetCore.SignalR;
using Multiplayer.Hubs;

namespace DnDiscordAPI.Campaign;

public sealed class CampaignRealtimeNotifier : ICampaignRealtimeNotifier
{
    private readonly IHubContext<GameHub> _gameHub;

    public CampaignRealtimeNotifier(IHubContext<GameHub> gameHub)
    {
        _gameHub = gameHub;
    }

    public Task NotifySessionCompletedAsync(Guid campaignId, Guid sessionId, CancellationToken ct = default)
    {
        return _gameHub.Clients.Group($"campaign_{campaignId:N}").SendAsync(
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

