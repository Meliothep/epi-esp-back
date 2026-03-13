using DnDiscord.Campaign.DataAccess;
using DnDiscordAPI.Games.Character.Repositories;
using DnDiscordAPI.Games.Character.Services;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Multiplayer.Services;

namespace DnDiscordAPI.Games
{
    public static class Extention
    {
        public static IHostApplicationBuilder AddGamesServices(this IHostApplicationBuilder builder, IConfiguration configuration)
        {
            // Enregistrement du DbContext avec PostgreSQL (sans pooling pour éviter les problèmes de configuration)
            builder.Services.AddDbContext<GamesDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(GamesDbContext).Assembly.FullName));
                // Ignore pending model changes warning in development
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Enregistrement d'AutoMapper
            builder.Services.AddAutoMapper(typeof(Character.Mappings.CharacterMappingProfile).Assembly);

            // Enregistrement des services
            builder.Services.AddScoped<ICharacterService, CharacterService>();
            builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
            builder.Services.AddScoped<ICharacterLookupService, CharacterLookupAdapter>();

            return builder;
        }
        /// <summary>
        /// Configures Campaign module endpoints and middleware.
        /// </summary>
        /// <param name="app">The web application.</param>
        /// <returns>The application for chaining.</returns>
        public static WebApplication UseGamesModule(this WebApplication app)
        {
            // Apply pending migrations (always, for Docker setup)
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
            return app;

        }
    }
}

