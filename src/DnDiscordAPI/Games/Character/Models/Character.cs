namespace DnDiscordAPI.Games.Character.Models
{
    public class Character
    {
        public Guid Id { get; set; }
        public string DiscordUserId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string Class { get; set; }
        public string Race { get; set; }
        public AbilityScores Abilities { get; set; }
        public int MaxHitPoints { get; set; }
        public int CurrentHitPoints { get; set; }
        public int ArmorClass { get; set; }
        public int Speed { get; set; }
        public int Initiative { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
