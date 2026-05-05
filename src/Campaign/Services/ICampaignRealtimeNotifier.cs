using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnDiscord.Campaign.Services;

/// <summary>
/// Abstraction to emit realtime campaign events (implemented in the API host
/// where SignalR hubs are available).
/// </summary>
public interface ICampaignRealtimeNotifier
{
    Task NotifySessionCompletedAsync(Guid campaignId, Guid sessionId, CancellationToken ct = default);
}

