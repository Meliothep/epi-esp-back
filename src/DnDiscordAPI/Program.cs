using System.Text;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Multiplayer.Extensions;
using DnDiscord.Campaign;
using DnDiscord.Campaign.DataAccess;
using DnDiscordAPI.Auth;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Games;
using DnDiscordAPI.Games.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Multiplayer.Hubs;
using DnDiscordAPI.Messages.Hubs;
using DnDiscordAPI.Messages.Services;

var builder = WebApplication.CreateBuilder(args);

// Ajout des services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddMultiplayerServices();

builder.AddGamesServices();
builder.AddAuthServices();

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

// Configuration Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "DnDiscord API", Version = "v1" });
});

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

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddSingleton<SignalRService>(); // Messages → front via SignalR

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

builder.AddGamesServices(builder.Configuration);

builder.Services.AddCampaignModule(builder.Configuration);

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseHttpsRedirection();

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

// Auth pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<GameHub>("/hubs/game").RequireCors("AllowFrontend");
app.MapHub<MessageHub>("/hubs/messages").RequireCors("AllowFrontend");

app.MapControllers();
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();
