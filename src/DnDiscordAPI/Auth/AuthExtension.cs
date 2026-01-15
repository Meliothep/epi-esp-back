using DnDiscordAPI.Auth.Services;

namespace DnDiscordAPI.Auth;

public static class AuthExtension
{
    public static IHostApplicationBuilder AddAuthServices(this IHostApplicationBuilder builder)
    {
        // Register HttpClient for Discord API calls
        builder.Services.AddHttpClient<IDiscordAuthService, DiscordAuthService>();
        
        // Register token service
        builder.Services.AddScoped<ITokenService, TokenService>();

        return builder;
    }
}

