using AutoMapper;
using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Models;

namespace DnDiscordAPI.Games.Character.Mappings
{
    public class CharacterMappingProfile : Profile
    {
        public CharacterMappingProfile()
        {
            // Character mappings
            CreateMap<Models.Character, CharacterDto>();
            CreateMap<CharacterDto, Models.Character>();

            // AbilityScores mappings
            CreateMap<AbilityScores, AbilityScoresDto>();
            CreateMap<AbilityScoresDto, AbilityScores>();
        }
    }
}

