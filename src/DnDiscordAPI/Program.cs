using System.Text;
using DnDiscord.Campaign;
using DnDiscordAPI.Auth;
using DnDiscordAPI.Auth.Services;
using DnDiscordAPI.Games;
using DnDiscordAPI.Games.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Ajout des services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.AddGamesServices();
builder.AddAuthServices();

// CORS configuration
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() 
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
    try
    {
        app.Logger.LogInformation("Applying database migrations...");
        dbContext.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied successfully.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
    {
        if (app.Environment.IsProduction())
        {
            throw;
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while applying database migrations.");
        if (app.Environment.IsProduction())
        {
            throw;
        }
    }
}

// Configure modular middleware
app.UseCors("AllowFrontend");

app.UseCampaignModule();
app.MapDefaultEndpoints();
app.UseHttpsRedirection();

// Auth pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
