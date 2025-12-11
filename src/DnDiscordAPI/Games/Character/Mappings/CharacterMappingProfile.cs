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
<<<<<<< Updated upstream
            CreateMap<Models.Character, CharacterDto>()
                .ForMember(dest => dest.RaceTraits, opt => opt.MapFrom(src => src.GetRaceTraits()))
                .ForMember(dest => dest.ClassTraits, opt => opt.MapFrom(src => src.GetClassTraits()));
            
=======
            CreateMap<Models.Character, CharacterDto>();
>>>>>>> Stashed changes
            CreateMap<CharacterDto, Models.Character>();

            // AbilityScores mappings
            CreateMap<AbilityScores, AbilityScoresDto>();
            CreateMap<AbilityScoresDto, AbilityScores>();
<<<<<<< Updated upstream

            // Traits mappings
            CreateMap<RaceTraits, RaceTraitsDto>();
            CreateMap<ClassTraits, ClassTraitsDto>();
=======
>>>>>>> Stashed changes
        }
    }
}

