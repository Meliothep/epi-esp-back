using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Multiplayer.Hubs;

/// <summary>
/// Resolves the SignalR user id from the Discord JWT's <c>sub</c> claim (the Discord
/// snowflake), falling back to <see cref="ClaimTypes.NameIdentifier"/>. Required so
/// <c>Clients.User(discordUserId)</c> routes messages to that specific connection group.
/// <para>
/// IMPORTANT: <c>Clients.User(...)</c> expects the Discord snowflake string (e.g.
/// <c>"665259540085735455"</c>), NOT the MD5-derived <see cref="System.Guid"/> stored
/// in <c>SessionPlayer.UserId</c> / <c>GameSession.DmUserId</c>. Passing the Guid
/// silently routes to nobody. For per-player delivery from a hub method, prefer
/// <c>Clients.Client(player.ConnectionId)</c> (the existing codebase pattern), or
/// look up the snowflake from the Discord identity claim if you genuinely need the
/// user-scoped fanout (e.g. cross-connection wallet broadcasts).
/// </para>
/// </summary>
public class DiscordUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var user = connection.User;
        return user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
