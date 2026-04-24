namespace DnDiscordAPI.Games.Character.Models
{
    public static class CharacterClassExtensions
    {
        // Classes with a matching 3D asset in the KayKit Adventurers pack.
        // Keep in sync with the frontend PLAYABLE_CLASSES set.
        private static readonly HashSet<CharacterClass> PlayableClasses = new()
        {
            CharacterClass.Barbare,
            CharacterClass.Guerrier,
            CharacterClass.Magicien,
            CharacterClass.Rodeur,
            CharacterClass.Voleur,
        };

        public static bool IsPlayable(this CharacterClass characterClass)
            => PlayableClasses.Contains(characterClass);

        public static IReadOnlyCollection<CharacterClass> GetPlayable()
            => PlayableClasses;
    }
}
