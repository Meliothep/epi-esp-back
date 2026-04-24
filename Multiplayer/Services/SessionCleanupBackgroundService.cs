using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Multiplayer.Services;

/// <summary>
/// E2.2: Periodically cleans up stale sessions (DM disconnected &gt; 5 min, or inactive &gt; 10 min).
/// </summary>
public class SessionCleanupBackgroundService : BackgroundService
{
    private readonly SessionManager _sessionManager;
    private readonly StateManager _stateManager;
    private readonly ILogger<SessionCleanupBackgroundService> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DmDisconnectedThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InactivityThreshold = TimeSpan.FromMinutes(10);

    public SessionCleanupBackgroundService(
        SessionManager sessionManager,
        StateManager stateManager,
        ILogger<SessionCleanupBackgroundService> logger)
    {
        _sessionManager = sessionManager;
        _stateManager = stateManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup background service started (interval: {Interval}, DM timeout: {DmTimeout}, inactivity: {Inactivity})",
            RunInterval, DmDisconnectedThreshold, InactivityThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RunInterval, stoppingToken);
                var removedIds = _sessionManager.CleanupStaleSessionsByPolicy(DmDisconnectedThreshold, InactivityThreshold);
                if (removedIds.Count > 0)
                {
                    foreach (var id in removedIds)
                        _stateManager.ClearSnapshot(id);
                    _logger.LogInformation("Session cleanup removed {Count} stale session(s)", removedIds.Count);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session cleanup");
            }
        }
    }
}
