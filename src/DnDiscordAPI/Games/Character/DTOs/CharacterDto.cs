using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DnDiscordAPI.Games.Character.Models;

namespace DnDiscordAPI.Games.Character.DTOs
{
    public class CharacterDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CharacterClass Class { get; set; }
        
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CharacterRace Race { get; set; }
        
        public int CurrentHitPoints { get; set; }
        public int MaxHitPoints { get; set; }
        public int ArmorClass { get; set; }
        public int Initiative { get; set; }
        public int Speed { get; set; }
        public AbilityScoresDto Abilities { get; set; }
        public RaceTraitsDto RaceTraits { get; set; }
        public ClassTraitsDto ClassTraits { get; set; }
    }


    public class CreateCharacterRequest
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CharacterClass Class { get; set; }

        [Required]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CharacterRace Race { get; set; }

        public AbilityScoresDto Abilities { get; set; }
    }

    public class RaceTraitsDto
    {
        public int StrengthModifier { get; set; }
        public int DexterityModifier { get; set; }
        public int ConstitutionModifier { get; set; }
        public int IntelligenceModifier { get; set; }
        public int WisdomModifier { get; set; }
        public int CharismaModifier { get; set; }
        public int BaseSpeed { get; set; }
        public string[] SpecialAbilities { get; set; }
    }

    public class ClassTraitsDto
    {
        public string MainCharacteristic { get; set; }
        public string HitDie { get; set; }
        public string[] SavingThrows { get; set; }
        public string[] Proficiencies { get; set; }
        public bool IsSpellcaster { get; set; }
        public string[] SpecialFeatures { get; set; }
    }

    public class AbilityScoresDto
    {
        [Range(1, 20)]
        public int Strength { get; set; } = 10;

        [Range(1, 20)]
        public int Dexterity { get; set; } = 10;

        [Range(1, 20)]
        public int Constitution { get; set; } = 10;

        [Range(1, 20)]
        public int Intelligence { get; set; } = 10;

        [Range(1, 20)]
        public int Wisdom { get; set; } = 10;

        [Range(1, 20)]
        public int Charisma { get; set; } = 10;
    }

    public class UpdateHitPointsRequest
    {
        [Range(0, int.MaxValue)]
        public int HitPoints { get; set; }
    }
}
