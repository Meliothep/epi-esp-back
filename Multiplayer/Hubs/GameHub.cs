using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Multiplayer.Hubs;

[Authorize]
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;

    public GameHub(ILogger<GameHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.User?.FindFirst("sub")?.Value
                     ?? "Unknown";

        _logger.LogInformation(
            "User {UserId} connected with ConnectionId {ConnectionId}",
            userId,
            Context.ConnectionId
        );

        await Clients.Caller.SendAsync("Connected", new
        {
            connectionId = Context.ConnectionId,
            userId = userId,
            timestamp = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";

        _logger.LogInformation(
            "User {UserId} disconnected. ConnectionId: {ConnectionId}. Exception: {Exception}",
            userId,
            Context.ConnectionId,
            exception?.Message ?? "None"
        );

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Ping()
    {
        _logger.LogDebug("Ping received from {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }
}