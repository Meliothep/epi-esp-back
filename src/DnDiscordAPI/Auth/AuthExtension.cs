using Microsoft.AspNetCore.Builder;
using DnDiscordAPI.Auth.Services;

namespace DnDiscordAPI.Auth;

public static class AuthExtension
{
    public static WebApplicationBuilder AddAuthServices(this WebApplicationBuilder builder)
    {
        // Register HttpClient for Discord API calls
        builder.Services.AddHttpClient<IDiscordAuthService, DiscordAuthService>();
        
        // Register token service
        builder.Services.AddScoped<ITokenService, TokenService>();

        // In-memory user store (shared between Auth and Discord Activity)
        builder.Services.AddSingleton<IUserStore, UserStoreService>();

        return builder;
    }
}

