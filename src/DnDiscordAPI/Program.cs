using DnDiscord.Campaign;
using DnDiscordAPI.Games;

var builder = WebApplication.CreateBuilder(args);

// Ajout des services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Ajout des services Games (DbContext, AutoMapper, Services)
builder.AddGamesServices();

builder.AddObservability();
builder.AddApiDefaults();

var app = builder.Build();

// Configure modular middleware
app.UseCampaignModule();
app.MapDefaultEndpoints();
app.UseHttpsRedirection();

// Note: Authentication temporairement désactivée pour les tests
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

app.Run();
