using DnDiscord.Campaign;
using DnDiscordAPI.Games;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Ajout des services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Ajout des services Games (DbContext, AutoMapper, Services)
builder.AddGamesServices();

builder.AddObservability();
builder.AddApiDefaults();

var app = builder.Build();

// Appliquer les migrations automatiquement au démarrage
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
    try
    {
        app.Logger.LogInformation("Applying database migrations...");
        dbContext.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while applying database migrations.");
        throw;
    }
}

// Configure modular middleware
app.UseCampaignModule();
app.MapDefaultEndpoints();
app.UseHttpsRedirection();

// Note: Authentication temporairement désactivée pour les tests
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

app.Run();
