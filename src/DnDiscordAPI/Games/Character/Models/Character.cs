using System.ComponentModel.DataAnnotations;

namespace DnDiscordAPI.Games.Character.Models
{
    public class Character
    {
        public Guid Id { get; set; }
        public string DiscordUserId { get; set; }
        public string Name { get; set; }
        [ConcurrencyCheck]
        public int Level { get; set; }
        [ConcurrencyCheck]
        public int ExperiencePoints { get; set; }
        public int AsiAppliedCount { get; set; } = 0;
        public CharacterClass Class { get; set; }
        public CharacterRace Race { get; set; }
        public AbilityScores Abilities { get; set; }
        public Wallet Wallet { get; set; }
        public int MaxHitPoints { get; set; }
        public int CurrentHitPoints { get; set; }
        public int ArmorClass { get; set; }
        public int Speed { get; set; }
        public int Initiative { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Obtient les traits de race du personnage
        /// </summary>
        public RaceTraits GetRaceTraits() => RaceTraits.GetTraits(Race);

        /// <summary>
        /// Obtient les traits de classe du personnage
        /// </summary>
        public ClassTraits GetClassTraits() => ClassTraits.GetTraits(Class);
    }
}
