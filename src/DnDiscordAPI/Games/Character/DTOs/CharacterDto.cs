using System.ComponentModel.DataAnnotations;

namespace DnDiscordAPI.Games.Character.DTOs
{
    public class CharacterDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Class { get; set; }
        public string Race { get; set; }
        public int CurrentHitPoints { get; set; }
        public int MaxHitPoints { get; set; }
        public int ArmorClass { get; set; }
        public int Initiative { get; set; }
        public AbilityScoresDto Abilities { get; set; }
    }


    public class CreateCharacterRequest
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Class { get; set; }

        [Required]
        public string Race { get; set; }

        public AbilityScoresDto Abilities { get; set; }
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
