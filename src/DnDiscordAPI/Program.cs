using DnDiscord.Campaign;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.AddObservability();

builder.AddApiDefaults();

// Add modular services
builder.AddCampaignModule();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure modular middleware
app.UseCampaignModule();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


public partial class DnDiscordAPIProgram { }
