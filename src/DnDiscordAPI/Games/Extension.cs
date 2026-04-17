using DnDiscordAPI.Games.Character.Repositories;
using DnDiscordAPI.Games.Character.Services;
using DnDiscordAPI.Games.Database;
using DnDiscordAPI.Games.Inventory.Services;
using Microsoft.EntityFrameworkCore;
using Multiplayer.Services;

namespace DnDiscordAPI.Games;

public static class GamesExtensions
{
    public static IHostApplicationBuilder AddGamesModule(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("gamesdb");

        builder.Services.AddDbContext<GamesDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddAutoMapper(typeof(Character.Mappings.CharacterMappingProfile).Assembly);

        builder.Services.AddScoped<ICharacterService, CharacterService>();
        builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
        builder.Services.AddScoped<ICharacterLookupService, CharacterLookupAdapter>();
        builder.Services.AddScoped<IInventoryService, InventoryService>();

        return builder;
    }

    public static WebApplication UseGamesModule(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GamesDbContext>();
        try
        {
            app.Logger.LogInformation("Applying Games database migrations...");
            dbContext.Database.Migrate();
            app.Logger.LogInformation("Games database migrations applied successfully.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("PendingModelChangesWarning"))
        {
            if (app.Environment.IsProduction()) throw;
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "An error occurred while applying Games database migrations.");
            if (app.Environment.IsProduction()) throw;
        }

        return app;
    }
}
