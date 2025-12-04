namespace DnDiscordAPI.Games.Character.Models
{
    /// <summary>
    /// Traits et caractéristiques associés à une classe
    /// </summary>
    public class ClassTraits
    {
        public CharacterClass Class { get; }
        public string MainCharacteristic { get; }
        public string HitDie { get; }
        public string[] SavingThrows { get; }
        public string[] Proficiencies { get; }
        public bool IsSpellcaster { get; }
        public string[] SpecialFeatures { get; }

        private ClassTraits(
            CharacterClass characterClass,
            string mainCharacteristic,
            string hitDie,
            string[] savingThrows,
            string[] proficiencies,
            bool isSpellcaster,
            params string[] specialFeatures)
        {
            Class = characterClass;
            MainCharacteristic = mainCharacteristic;
            HitDie = hitDie;
            SavingThrows = savingThrows;
            Proficiencies = proficiencies;
            IsSpellcaster = isSpellcaster;
            SpecialFeatures = specialFeatures;
        }

        /// <summary>
        /// Obtient les traits pour une classe donnée
        /// </summary>
        public static ClassTraits GetTraits(CharacterClass characterClass)
        {
            return characterClass switch
            {
                CharacterClass.Guerrier => new ClassTraits(
                    characterClass: CharacterClass.Guerrier,
                    mainCharacteristic: "Force ou Dextérité",
                    hitDie: "d10",
                    savingThrows: new[] { "Force", "Constitution" },
                    proficiencies: new[] { "Armes simples & martiales", "Toutes armures", "Boucliers" },
                    isSpellcaster: false,
                    specialFeatures: new[] { "Style de combat", "Attaques multiples" }
                ),

                CharacterClass.Barbare => new ClassTraits(
                    characterClass: CharacterClass.Barbare,
                    mainCharacteristic: "Force",
                    hitDie: "d12",
                    savingThrows: new[] { "Force", "Constitution" },
                    proficiencies: new[] { "Armes simples & martiales", "Armures légères & moyennes", "Boucliers" },
                    isSpellcaster: false,
                    specialFeatures: new[] { "Rage", "Défense sans armure", "Danger sense" }
                ),

                CharacterClass.Barde => new ClassTraits(
                    characterClass: CharacterClass.Barde,
                    mainCharacteristic: "Charisme",
                    hitDie: "d8",
                    savingThrows: new[] { "Dextérité", "Charisme" },
                    proficiencies: new[] { "Armes simples", "Armures légères", "Instruments de musique" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Inspiration bardique", "Touche-à-tout", "Sorts" }
                ),

                CharacterClass.Clerc => new ClassTraits(
                    characterClass: CharacterClass.Clerc,
                    mainCharacteristic: "Sagesse",
                    hitDie: "d8",
                    savingThrows: new[] { "Sagesse", "Charisme" },
                    proficiencies: new[] { "Armes simples", "Armures légères & moyennes", "Boucliers" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Canalisation d'énergie divine", "Domaine divin", "Sorts" }
                ),

                CharacterClass.Druide => new ClassTraits(
                    characterClass: CharacterClass.Druide,
                    mainCharacteristic: "Sagesse",
                    hitDie: "d8",
                    savingThrows: new[] { "Intelligence", "Sagesse" },
                    proficiencies: new[] { "Armes simples", "Armures légères & moyennes", "Boucliers" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Forme sauvage", "Cercle druidique", "Sorts" }
                ),

                CharacterClass.Moine => new ClassTraits(
                    characterClass: CharacterClass.Moine,
                    mainCharacteristic: "Dextérité et Sagesse",
                    hitDie: "d8",
                    savingThrows: new[] { "Force", "Dextérité" },
                    proficiencies: new[] { "Armes simples", "Épées courtes" },
                    isSpellcaster: false,
                    specialFeatures: new[] { "Arts martiaux", "Ki", "Défense sans armure", "Déplacement sans armure" }
                ),

                CharacterClass.Paladin => new ClassTraits(
                    characterClass: CharacterClass.Paladin,
                    mainCharacteristic: "Force et Charisme",
                    hitDie: "d10",
                    savingThrows: new[] { "Sagesse", "Charisme" },
                    proficiencies: new[] { "Armes simples & martiales", "Toutes armures", "Boucliers" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Sens divin", "Imposition des mains", "Serment sacré", "Châtiment divin" }
                ),

                CharacterClass.Rodeur => new ClassTraits(
                    characterClass: CharacterClass.Rodeur,
                    mainCharacteristic: "Dextérité et Sagesse",
                    hitDie: "d10",
                    savingThrows: new[] { "Force", "Dextérité" },
                    proficiencies: new[] { "Armes simples & martiales", "Armures légères & moyennes", "Boucliers" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Ennemi juré", "Explorateur-né", "Sorts" }
                ),

                CharacterClass.Voleur => new ClassTraits(
                    characterClass: CharacterClass.Voleur,
                    mainCharacteristic: "Dextérité",
                    hitDie: "d8",
                    savingThrows: new[] { "Dextérité", "Intelligence" },
                    proficiencies: new[] { "Armes simples", "Arbalète de poing", "Épées courtes & longues", "Armures légères" },
                    isSpellcaster: false,
                    specialFeatures: new[] { "Attaque sournoise", "Ruse", "Expertise" }
                ),

                CharacterClass.Ensorceleur => new ClassTraits(
                    characterClass: CharacterClass.Ensorceleur,
                    mainCharacteristic: "Charisme",
                    hitDie: "d6",
                    savingThrows: new[] { "Constitution", "Charisme" },
                    proficiencies: new[] { "Dagues", "Fléchettes", "Frondes", "Bâtons", "Arbalètes légères" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Origine magique", "Métamagie", "Points de sorcellerie", "Sorts" }
                ),

                CharacterClass.Sorcier => new ClassTraits(
                    characterClass: CharacterClass.Sorcier,
                    mainCharacteristic: "Charisme",
                    hitDie: "d8",
                    savingThrows: new[] { "Sagesse", "Charisme" },
                    proficiencies: new[] { "Armes simples", "Armures légères" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Pacte occulte", "Invocations occultes", "Sorts" }
                ),

                CharacterClass.Magicien => new ClassTraits(
                    characterClass: CharacterClass.Magicien,
                    mainCharacteristic: "Intelligence",
                    hitDie: "d6",
                    savingThrows: new[] { "Intelligence", "Sagesse" },
                    proficiencies: new[] { "Dagues", "Fléchettes", "Frondes", "Bâtons", "Arbalètes légères" },
                    isSpellcaster: true,
                    specialFeatures: new[] { "Grimoire", "École de magie", "Récupération arcanique", "Sorts" }
                ),

                _ => throw new ArgumentException($"Classe inconnue: {characterClass}")
            };
        }

        /// <summary>
        /// Calcule les points de vie maximaux pour un niveau donné
        /// </summary>
        public int CalculateMaxHitPoints(int level, int constitutionModifier)
        {
            var hitDieValue = HitDie switch
            {
                "d6" => 6,
                "d8" => 8,
                "d10" => 10,
                "d12" => 12,
                _ => 8
            };

            // Niveau 1 : Maximum du dé + modificateur de Constitution
            // Niveaux suivants : Moyenne du dé + modificateur de Constitution
            var level1Hp = hitDieValue + constitutionModifier;
            var additionalLevelsHp = (level - 1) * ((hitDieValue / 2) + 1 + constitutionModifier);
            
            return level1Hp + additionalLevelsHp;
        }
    }
}

