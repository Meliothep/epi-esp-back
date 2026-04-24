using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Multiplayer.Hubs;

/// <summary>
/// Resolves the SignalR user id from the Discord JWT's <c>sub</c> claim (the Discord
/// snowflake), falling back to <see cref="ClaimTypes.NameIdentifier"/>. Required so
/// <c>Clients.User(discordUserId)</c> routes messages to that specific connection group.
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
