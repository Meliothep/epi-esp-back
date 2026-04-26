namespace DnDiscordAPI.Games.Character.Models
{
    /// <summary>
    /// Traits et modificateurs associés à une race
    /// </summary>
    public class RaceTraits
    {
        public CharacterRace Race { get; }
        public int StrengthModifier { get; }
        public int DexterityModifier { get; }
        public int ConstitutionModifier { get; }
        public int IntelligenceModifier { get; }
        public int WisdomModifier { get; }
        public int CharismaModifier { get; }
        public int BaseSpeed { get; }
        public string[] SpecialAbilities { get; }

        private RaceTraits(
            CharacterRace race,
            int strength = 0,
            int dexterity = 0,
            int constitution = 0,
            int intelligence = 0,
            int wisdom = 0,
            int charisma = 0,
            int baseSpeed = 30,
            params string[] specialAbilities)
        {
            Race = race;
            StrengthModifier = strength;
            DexterityModifier = dexterity;
            ConstitutionModifier = constitution;
            IntelligenceModifier = intelligence;
            WisdomModifier = wisdom;
            CharismaModifier = charisma;
            BaseSpeed = baseSpeed;
            SpecialAbilities = specialAbilities;
        }

        /// <summary>
        /// Obtient les traits pour une race donnée
        /// </summary>
        public static RaceTraits GetTraits(CharacterRace race)
        {
            return race switch
            {
                CharacterRace.Humain => new RaceTraits(
                    race: CharacterRace.Humain,
                    strength: 1,
                    dexterity: 1,
                    constitution: 1,
                    intelligence: 1,
                    wisdom: 1,
                    charisma: 1,
                    baseSpeed: 30,
                    specialAbilities: new[] { "Polyvalence" }
                ),
                
                CharacterRace.Elfe => new RaceTraits(
                    race: CharacterRace.Elfe,
                    dexterity: 2,
                    baseSpeed: 30,
                    specialAbilities: new[] { "Vision dans le noir", "Sens aiguisés", "Ascendance féerique", "Transe" }
                ),
                
                CharacterRace.Nain => new RaceTraits(
                    race: CharacterRace.Nain,
                    constitution: 2,
                    baseSpeed: 25,
                    specialAbilities: new[] { "Vision dans le noir", "Résistance naine", "Connaissance de la pierre", "Formation aux armes naines" }
                ),
                
                CharacterRace.Halfelin => new RaceTraits(
                    race: CharacterRace.Halfelin,
                    dexterity: 2,
                    baseSpeed: 25,
                    specialAbilities: new[] { "Chanceux", "Brave", "Agilité halfeline" }
                ),
                
                CharacterRace.DemiOrc => new RaceTraits(
                    race: CharacterRace.DemiOrc,
                    strength: 2,
                    constitution: 1,
                    baseSpeed: 30,
                    specialAbilities: new[] { "Vision dans le noir", "Menaçant", "Endurance implacable", "Attaques sauvages" }
                ),
                
                CharacterRace.Tieffelin => new RaceTraits(
                    race: CharacterRace.Tieffelin,
                    intelligence: 1,
                    charisma: 2,
                    baseSpeed: 30,
                    specialAbilities: new[] { "Vision dans le noir", "Résistance infernale", "Héritage infernal" }
                ),
                
                CharacterRace.Gnome => new RaceTraits(
                    race: CharacterRace.Gnome,
                    intelligence: 2,
                    baseSpeed: 25,
                    specialAbilities: new[] { "Vision dans le noir", "Ruse gnome" }
                ),
                
                _ => throw new ArgumentException($"Race inconnue: {race}")
            };
        }

        /// <summary>
        /// Applique les modificateurs raciaux aux scores d'aptitude
        /// </summary>
        public AbilityScores ApplyModifiers(AbilityScores baseScores)
        {
            return new AbilityScores
            {
                Strength = baseScores.Strength + StrengthModifier,
                Dexterity = baseScores.Dexterity + DexterityModifier,
                Constitution = baseScores.Constitution + ConstitutionModifier,
                Intelligence = baseScores.Intelligence + IntelligenceModifier,
                Wisdom = baseScores.Wisdom + WisdomModifier,
                Charisma = baseScores.Charisma + CharismaModifier
            };
        }
    }
}

