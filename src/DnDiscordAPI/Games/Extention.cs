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
            // Enregistrement du DbContext avec PostgreSQL
            builder.AddNpgsqlDbContext<GamesDbContext>("gamesdb");

            // Enregistrement d'AutoMapper
            builder.Services.AddAutoMapper(typeof(Character.Mappings.CharacterMappingProfile).Assembly);

            // Enregistrement des services
            builder.Services.AddScoped<ICharacterService, CharacterService>();
            builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();

            return builder;
        }
    }
}

