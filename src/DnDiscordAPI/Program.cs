using System.Text;
using DnDiscord.Campaign;
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

// Enregistrement des services d'authentification
builder.Services.AddHttpClient<IDiscordAuthService, DiscordAuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();

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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true
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

app.UseCampaignModule();
app.MapDefaultEndpoints();
app.UseHttpsRedirection();

// Auth pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
