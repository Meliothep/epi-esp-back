using Microsoft.Extensions.DependencyInjection;
using Multiplayer.Services;

namespace Multiplayer.Extensions;

public static class MultiplayerServiceExtensions
{
    public static IServiceCollection AddMultiplayerServices(
        this IServiceCollection services)
    {
        // Services Singleton (thread-safe)
        services.AddSingleton<StateManager>();
        services.AddSingleton<SessionUnitPositionStore>();
        services.AddSingleton<IGameActionValidator, GameActionValidator>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<TurnManager>();
        services.AddSingleton<MessageSequencer>();

        // Nettoyage des sessions inactives en arrière-plan
        services.AddHostedService<SessionCleanupBackgroundService>();

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 64 * 1024; // 64KB limit
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        })
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        return services;
    }
}