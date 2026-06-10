using System.Text;
using System.Threading.RateLimiting;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Multiplayer.Extensions;
using DnDiscord.Campaign;
using DnDiscordAPI.Auth;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Games;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Multiplayer.Hubs;
using DnDiscordAPI.Messages.Hubs;
using DnDiscordAPI.Messages.Services;
using DnDiscordAPI.PartyChat;
using DnDiscordAPI;
using DnDiscordAPI.Games.Database;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.Services;
using DnDiscordAPI.Campaign;

var builder = WebApplication.CreateBuilder(args);

// Ajout des services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddMultiplayerServices();

builder.AddAuthServices();
builder.AddGamesModule();
builder.AddCampaignModule();

// Campaign realtime events are emitted through the GameHub, which only exists
// in the API host project. Controllers/services inside the Campaign module
// consume the abstraction.
builder.Services.AddSingleton<ICampaignRealtimeNotifier, CampaignRealtimeNotifier>();

// Rate limiting global léger pour le POC ; policies fines pour les
// endpoints destructifs / coûteux RGPD (DELETE /me et GET /me/export).
// Clé de partition : sub claim (per-user). Fallback : ip si non
// authentifié, ce qui limite aussi le bruit anonymous.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("rgpd-destructive", httpContext =>
    {
        var key = httpContext.User?.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            });
    });

    options.AddPolicy("rgpd-export", httpContext =>
    {
        var key = httpContext.User?.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            });
    });
});


// CORS configuration
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() 
    ?? new[] { "http://localhost:3000" };
var corsOriginsSet = new HashSet<string>(corsOrigins, StringComparer.OrdinalIgnoreCase);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                try
                {
                    var uri = new Uri(origin);
                    if (corsOriginsSet.Contains(origin)) return true;
                    // Activités Discord : *.discordsays.com
                    var host = uri.Host;
                    return host.Equals("discordsays.com", StringComparison.OrdinalIgnoreCase)
                        || host.EndsWith(".discordsays.com", StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// API documentation UI: Scalar, mapped in ServiceDefaults via MapScalarApiReference.
// Reachable at http://localhost:5054/scalar/v1 (no /swagger endpoint — the SwaggerGen
// registration that used to live here was never paired with UseSwagger/UseSwaggerUI
// middleware, so it did nothing; Scalar is the only live docs surface).
string? scalarURL = Environment.GetEnvironmentVariable("SCALAR_URLS");
scalarURL = scalarURL != null ? scalarURL : "http://localhost:5054";
builder.AddObservability();
builder.AddApiDefaults();

// Configuration Authentication/Authorization (JWT)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey must be configured");
var jwtIssuer = jwtSection["Issuer"] ?? "dndiscord-backend";
var jwtAudience = jwtSection["Audience"] ?? "dndiscord-frontend";

builder.Services.AddSingleton<SignalRService>(); // Messages → front via SignalR
builder.Services.AddSingleton<VoiceSessionRegistry>(); 

builder.Services.AddHttpClient("discord-bot", http =>
{
    http.BaseAddress = new Uri("https://discord.com/api/v10/");
    http.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<DnDiscordAPI.Discord.IDiscordBotNotifier, DnDiscordAPI.Discord.DiscordBotNotifier>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Disable automatic claim type mapping to preserve original JWT claim names
        // Without this, "sub" becomes "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
        options.MapInboundClaims = false;
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5) // Explicit tolerance for clock differences
        };

        // JWT Bearer debug events for troubleshooting authentication issues
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                
                var accessToken = context.Request.Query["access_token"];
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    var authHeader = context.Request.Headers["Authorization"].ToString();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        accessToken = authHeader.Substring("Bearer ".Length).Trim();
                    }
                }
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Authentication failed: {Error}", context.Exception.Message);
                if (context.Exception.InnerException != null)
                {
                    logger.LogWarning("Inner exception: {InnerError}", context.Exception.InnerException.Message);
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Challenge triggered. Error: {Error}, ErrorDescription: {ErrorDescription}", 
                    context.Error ?? "Unknown", 
                    context.ErrorDescription ?? "No description");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.Principal?.FindFirst("sub")?.Value;
                logger.LogInformation("JWT Token validated successfully for user: {UserId}", userId);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await services.GetRequiredService<GamesDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<CampaignDbContext>().Database.MigrateAsync();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("X-Frame-Options");
    context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'self' https://discord.com https://*.discord.com https://*.discordsays.com");
    
    await next();
});

// Configure modular middleware
app.UseCors("AllowFrontend");

app.UseGamesModule();
app.UseCampaignModule();
app.MapDefaultEndpoints();
app.UseHttpsRedirection();

// Auth pipeline
app.UseAuthentication();
// RGPD : bloque les JWT d'un compte supprimé avant de laisser la requête
// atteindre l'autorisation + les actions (cf. TombstonedAccountMiddleware).
app.UseMiddleware<DnDiscordAPI.Auth.TombstonedAccountMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHub<GameHub>("/hubs/game").RequireCors("AllowFrontend");
app.MapHub<MessageHub>("/hubs/messages").RequireCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.MapDevLogBridge();
}

app.MapControllers();
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();

// Expose entry point for WebApplicationFactory in integration tests
public partial class DnDiscordAPIProgram { }
