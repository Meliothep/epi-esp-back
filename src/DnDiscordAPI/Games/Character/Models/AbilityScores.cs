namespace DnDiscordAPI.Games.Character.Models
{
    public class AbilityScores
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        public int GetModifier(int score) => (score - 10) / 2;
    }
}
