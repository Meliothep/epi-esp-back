using DnDiscordAPI.Games.Character.Repositories;
using DnDiscordAPI.Games.Character.Services;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;

namespace DnDiscordAPI.Games
{
    public static class Extention
    {
        public static IHostApplicationBuilder AddGamesServices(this IHostApplicationBuilder builder)
        {
            // Enregistrement du DbContext avec PostgreSQL (sans pooling pour éviter les problèmes de configuration)
            var connectionString = builder.Configuration.GetConnectionString("gamesdb");
            builder.Services.AddDbContext<GamesDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                // Ignore pending model changes warning in development
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Enregistrement d'AutoMapper
            builder.Services.AddAutoMapper(typeof(Character.Mappings.CharacterMappingProfile).Assembly);

            // Enregistrement des services
            builder.Services.AddScoped<ICharacterService, CharacterService>();
            builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();

            return builder;
        }
    }
}

