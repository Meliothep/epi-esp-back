using Microsoft.Extensions.DependencyInjection;
using Multiplayer.Services;

namespace Multiplayer.Extensions;

public static class MultiplayerServiceExtensions
{
    public static IServiceCollection AddMultiplayerServices(
        this IServiceCollection services)
    {
        // Services Singleton (thread-safe)
        services.AddSingleton<SessionManager>();
        services.AddSingleton<TurnManager>();
        services.AddSingleton<MessageSequencer>();

        // Configuration SignalR
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}