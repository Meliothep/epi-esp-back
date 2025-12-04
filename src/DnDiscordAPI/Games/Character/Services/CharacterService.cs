using AutoMapper;
using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Models;
using DnDiscordAPI.Games.Database;
using Microsoft.EntityFrameworkCore;

namespace DnDiscordAPI.Games.Character.Services
{
    public interface ICharacterService
    {
        Task<CharacterDto> CreateCharacterAsync(string discordUserId, CreateCharacterRequest request);
        Task<CharacterDto> GetCharacterAsync(Guid characterId);
        Task<List<CharacterDto>> GetUserCharactersAsync(string discordUserId);
        Task<CharacterDto> UpdateHitPointsAsync(Guid characterId, int newHitPoints);
        Task<CharacterDto> LevelUpAsync(Guid characterId);
    }


    public class CharacterService : ICharacterService
    {
        private readonly GamesDbContext _context; // Manque la classe GamesDbContext
        private readonly IMapper _mapper; // Manque la référence à AutoMapper
        private readonly ILogger<CharacterService> _logger;

        public CharacterService(
            GamesDbContext context, 
            IMapper mapper,
            ILogger<CharacterService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<CharacterDto> CreateCharacterAsync(string discordUserId, CreateCharacterRequest request)
        {
            var character = new Models.Character
            {
                Id = Guid.NewGuid(),
                DiscordUserId = discordUserId,
                Name = request.Name,
                Class = request.Class,
                Race = request.Race,
                Level = 1,
                Abilities = _mapper.Map<AbilityScores>(request.Abilities),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Calculs initiaux
            character.MaxHitPoints = CalculateInitialHitPoints(character.Class, character.Abilities.GetModifier(character.Abilities.Constitution));
            character.CurrentHitPoints = character.MaxHitPoints;
            character.ArmorClass = 10 + character.Abilities.GetModifier(character.Abilities.Dexterity);
            character.Speed = GetRaceSpeed(character.Race);
            character.Initiative = character.Abilities.GetModifier(character.Abilities.Dexterity);

            _context.Characters.Add(character);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Character {Name} created for user {UserId}", character.Name, discordUserId);

            return _mapper.Map<CharacterDto>(character);
        }

        public async Task<List<CharacterDto>> GetUserCharactersAsync(string discordUserId)
        {
            var characters = await _context.Characters
                .Where(c => c.DiscordUserId == discordUserId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return _mapper.Map<List<CharacterDto>>(characters);
        }

        public async Task<CharacterDto> GetCharacterAsync(Guid characterId)
        {
            var character = await _context.Characters
                .FirstOrDefaultAsync(c => c.Id == characterId);

            if (character == null)
                throw new Exception($"Character {characterId} not found");

            return _mapper.Map<CharacterDto>(character);
        }

        public async Task<CharacterDto> UpdateHitPointsAsync(Guid characterId, int newHitPoints)
        {
            var character = await _context.Characters.FindAsync(characterId);
            if (character == null)
                throw new Exception($"Character {characterId} not found");

            character.CurrentHitPoints = Math.Clamp(newHitPoints, 0, character.MaxHitPoints);
            character.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return _mapper.Map<CharacterDto>(character);
        }



        private int CalculateInitialHitPoints(string characterClass, int constitutionModifier)
        {
            var baseHp = characterClass.ToLower() switch
            {
                "barbarian" => 12,
                "fighter" or "paladin" or "ranger" => 10,
                "bard" or "cleric" or "druid" or "monk" or "rogue" or "warlock" => 8,
                "sorcerer" or "wizard" => 6,
                _ => 8
            };

            return baseHp + constitutionModifier;
        }

        private int GetRaceSpeed(string race)
        {
            return race.ToLower() switch
            {
                "dwarf" or "halfling" or "gnome" => 25,
                "wood elf" => 35,
                _ => 30
            };
        }

        public async Task<CharacterDto> LevelUpAsync(Guid characterId)
        {
            var character = await _context.Characters.FindAsync(characterId);
            if (character == null)
                throw new Exception($"Character {characterId} not found");

            // Augmenter le niveau
            character.Level++;

            // Calculer l'augmentation des HP
            var hpIncrease = RollHitDie(character.Class) + character.Abilities.GetModifier(character.Abilities.Constitution);
            character.MaxHitPoints += hpIncrease;
            character.CurrentHitPoints += hpIncrease; // On augmente aussi les HP actuels

            var newProficiencyBonus = 2 + ((character.Level - 1) / 4);

            character.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Character {Name} (ID: {Id}) leveled up to level {Level}. HP: {Current}/{Max}",
                character.Name,
                character.Id,
                character.Level,
                character.CurrentHitPoints,
                character.MaxHitPoints
            );

            return _mapper.Map<CharacterDto>(character);
        }

        private int RollHitDie(string characterClass)
        {
            var dieSize = characterClass.ToLower() switch
            {
                "barbarian" => 12,
                "fighter" or "paladin" or "ranger" => 10,
                "bard" or "cleric" or "druid" or "monk" or "rogue" or "warlock" => 8,
                "sorcerer" or "wizard" => 6,
                _ => 8
            };

            return Random.Shared.Next(1, dieSize + 1);
        }
    }
}
