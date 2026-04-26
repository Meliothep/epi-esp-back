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
            CreateMap<Models.Character, CharacterDto>()
                .ForMember(dest => dest.RaceTraits, opt => opt.MapFrom(src => src.GetRaceTraits()))
                .ForMember(dest => dest.ClassTraits, opt => opt.MapFrom(src => src.GetClassTraits()));
            CreateMap<CharacterDto, Models.Character>();

            // AbilityScores mappings
            CreateMap<AbilityScores, AbilityScoresDto>();
            CreateMap<AbilityScoresDto, AbilityScores>();

            // Traits mappings
            CreateMap<RaceTraits, RaceTraitsDto>();
            CreateMap<ClassTraits, ClassTraitsDto>();

            // Wallet mappings
            CreateMap<Wallet, WalletDto>();
            CreateMap<WalletDto, Wallet>();
        }
    }
}
