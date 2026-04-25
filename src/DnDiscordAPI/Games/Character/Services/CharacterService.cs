using AutoMapper;
using DnDiscordAPI.Games.Character.DTOs;
using DnDiscordAPI.Games.Character.Models;
using DnDiscordAPI.Games.Database;
using DnDiscordAPI.Messages.Services;
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
        Task<WalletDto> GetWalletAsync(Guid characterId);
        Task<WalletDto> ModifyWalletAsync(Guid characterId, ModifyWalletRequest request);

        /// <summary>
        /// Returns the Discord user id string that owns the character, or null if the character
        /// doesn't exist. Projection-only query — doesn't materialise the full entity.
        /// </summary>
        Task<string?> GetOwnerDiscordIdAsync(Guid characterId);
    }


    public class CharacterService : ICharacterService
    {
        private readonly GamesDbContext _context;
        private readonly IMapper _mapper;
        private readonly SignalRService _signalR;
        private readonly ILogger<CharacterService> _logger;

        public CharacterService(
            GamesDbContext context,
            IMapper mapper,
            SignalRService signalR,
            ILogger<CharacterService> logger)
        {
            _context = context;
            _mapper = mapper;
            _signalR = signalR;
            _logger = logger;
        }

        public async Task<CharacterDto> CreateCharacterAsync(string discordUserId, CreateCharacterRequest request)
        {
            // Obtenir les traits de race et de classe
            var raceTraits = RaceTraits.GetTraits(request.Race);
            var classTraits = ClassTraits.GetTraits(request.Class);

            // Créer les scores d'aptitude de base
            var baseAbilities = _mapper.Map<AbilityScores>(request.Abilities ?? new AbilityScoresDto());
            
            // Appliquer les modificateurs raciaux
            var finalAbilities = raceTraits.ApplyModifiers(baseAbilities);

            var character = new Models.Character
            {
                Id = Guid.NewGuid(),
                DiscordUserId = discordUserId,
                Name = request.Name,
                Class = request.Class,
                Race = request.Race,
                Level = 1,
                ExperiencePoints = 0,
                Abilities = finalAbilities,
                Wallet = new Wallet(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Calculs initiaux basés sur les traits
            var constitutionModifier = character.Abilities.GetModifier(character.Abilities.Constitution);
            character.MaxHitPoints = classTraits.CalculateMaxHitPoints(1, constitutionModifier);
            character.CurrentHitPoints = character.MaxHitPoints;
            character.ArmorClass = 10 + character.Abilities.GetModifier(character.Abilities.Dexterity);
            character.Speed = raceTraits.BaseSpeed;
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
                throw new KeyNotFoundException($"Character {characterId} not found");

            return _mapper.Map<CharacterDto>(character);
        }

        public async Task<CharacterDto> UpdateHitPointsAsync(Guid characterId, int newHitPoints)
        {
            var character = await _context.Characters.FindAsync(characterId);
            if (character == null)
                throw new KeyNotFoundException($"Character {characterId} not found");

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
                throw new KeyNotFoundException($"Character {characterId} not found");

            var previousLevel = character.Level;

            // Augmenter le niveau
            character.Level++;

            // Lightweight stat progression: every 4 levels, grant an ability-score
            // increase according to class fantasy, capped to 20.
            ApplyAbilityScoreIncrease(character, _logger);

            // Obtenir les traits de classe pour le calcul des HP
            var classTraits = character.GetClassTraits();
            var constitutionModifier = character.Abilities.GetModifier(character.Abilities.Constitution);
            
            // Recalculer les HP maximaux pour le nouveau niveau
            var newMaxHp = classTraits.CalculateMaxHitPoints(character.Level, constitutionModifier);
            var hpIncrease = newMaxHp - character.MaxHitPoints;
            
            character.MaxHitPoints = newMaxHp;
            character.CurrentHitPoints = Math.Clamp(character.CurrentHitPoints + hpIncrease, 0, character.MaxHitPoints);

            // Keep derived combat stats in sync with updated abilities.
            character.ArmorClass = 10 + character.Abilities.GetModifier(character.Abilities.Dexterity);
            character.Initiative = character.Abilities.GetModifier(character.Abilities.Dexterity);

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

            _logger.LogDebug(
                "Character {Id} level-up details: {PreviousLevel}->{Level}, AC={ArmorClass}, Init={Initiative}, STR={Strength}, DEX={Dexterity}, CON={Constitution}, INT={Intelligence}, WIS={Wisdom}, CHA={Charisma}",
                character.Id,
                previousLevel,
                character.Level,
                character.ArmorClass,
                character.Initiative,
                character.Abilities.Strength,
                character.Abilities.Dexterity,
                character.Abilities.Constitution,
                character.Abilities.Intelligence,
                character.Abilities.Wisdom,
                character.Abilities.Charisma);

            return _mapper.Map<CharacterDto>(character);
        }

        private static int ClampAbility(int value) => Math.Clamp(value, 1, 20);

        private static void ApplyAbilityScoreIncrease(Models.Character character, ILogger<CharacterService> logger)
        {
            // D&D-like ASI cadence.
            if (character.Level % 4 != 0) return;

            switch (character.Class)
            {
                case Models.CharacterClass.Barbare:
                case Models.CharacterClass.Guerrier:
                case Models.CharacterClass.Paladin:
                    character.Abilities.Strength = ClampAbility(character.Abilities.Strength + 2);
                    break;

                case Models.CharacterClass.Voleur:
                case Models.CharacterClass.Rodeur:
                    character.Abilities.Dexterity = ClampAbility(character.Abilities.Dexterity + 2);
                    break;

                case Models.CharacterClass.Moine:
                    character.Abilities.Dexterity = ClampAbility(character.Abilities.Dexterity + 1);
                    character.Abilities.Wisdom = ClampAbility(character.Abilities.Wisdom + 1);
                    break;

                case Models.CharacterClass.Barde:
                case Models.CharacterClass.Ensorceleur:
                case Models.CharacterClass.Sorcier:
                    character.Abilities.Charisma = ClampAbility(character.Abilities.Charisma + 2);
                    break;

                case Models.CharacterClass.Clerc:
                case Models.CharacterClass.Druide:
                    character.Abilities.Wisdom = ClampAbility(character.Abilities.Wisdom + 2);
                    break;

                case Models.CharacterClass.Magicien:
                    character.Abilities.Intelligence = ClampAbility(character.Abilities.Intelligence + 2);
                    break;

                default:
                    logger.LogWarning("No ASI configured for character class {Class}. If a new class was added to the enum, update ApplyAbilityScoreIncrease.", character.Class);
                    break;
            }
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

        public Task<string?> GetOwnerDiscordIdAsync(Guid characterId)
        {
            return _context.Characters
                .AsNoTracking()
                .Where(c => c.Id == characterId)
                .Select(c => c.DiscordUserId)
                .FirstOrDefaultAsync();
        }

        public async Task<WalletDto> GetWalletAsync(Guid characterId)
        {
            var character = await _context.Characters.FindAsync(characterId)
                ?? throw new KeyNotFoundException($"Character {characterId} not found");

            return _mapper.Map<WalletDto>(character.Wallet ?? new Wallet());
        }

        public async Task<WalletDto> ModifyWalletAsync(Guid characterId, ModifyWalletRequest request)
        {
            var character = await _context.Characters.FindAsync(characterId)
                ?? throw new KeyNotFoundException($"Character {characterId} not found");

            character.Wallet ??= new Wallet();

            // Appliquer les deltas et clamper à 0
            character.Wallet.CopperPieces = Math.Max(0, character.Wallet.CopperPieces + request.CopperPieces);
            character.Wallet.SilverPieces = Math.Max(0, character.Wallet.SilverPieces + request.SilverPieces);
            character.Wallet.ElectrumPieces = Math.Max(0, character.Wallet.ElectrumPieces + request.ElectrumPieces);
            character.Wallet.GoldPieces = Math.Max(0, character.Wallet.GoldPieces + request.GoldPieces);
            character.Wallet.PlatinumPieces = Math.Max(0, character.Wallet.PlatinumPieces + request.PlatinumPieces);
            character.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var dto = _mapper.Map<WalletDto>(character.Wallet);

            await _signalR.SendWalletChangedAsync(character.DiscordUserId, characterId, dto);

            _logger.LogInformation(
                "Wallet modified for character {Character}: CP={CP} PA={PA} PE={PE} PO={PO} PP={PP}",
                characterId,
                character.Wallet.CopperPieces,
                character.Wallet.SilverPieces,
                character.Wallet.ElectrumPieces,
                character.Wallet.GoldPieces,
                character.Wallet.PlatinumPieces);

            return dto;
        }
    }
}
