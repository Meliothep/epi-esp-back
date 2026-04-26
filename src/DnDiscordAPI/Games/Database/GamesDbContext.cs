using DnDiscordAPI.Games.Character.Models;
using DnDiscordAPI.Games.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace DnDiscordAPI.Games.Database
{
    public class GamesDbContext : DbContext
    {
        public GamesDbContext(DbContextOptions<GamesDbContext> options) : base(options)
        {
        }

        public DbSet<Character.Models.Character> Characters { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<InventoryEntry> InventoryEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration pour Character
            modelBuilder.Entity<Character.Models.Character>(entity =>
            {
                entity.ToTable("Characters");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.DiscordUserId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Class)
                    .IsRequired();

                entity.Property(e => e.Race)
                    .IsRequired();

                // Configuration pour les AbilityScores (owned entity)
                entity.OwnsOne(e => e.Abilities, abilities =>
                {
                    abilities.Property(a => a.Strength).IsRequired();
                    abilities.Property(a => a.Dexterity).IsRequired();
                    abilities.Property(a => a.Constitution).IsRequired();
                    abilities.Property(a => a.Intelligence).IsRequired();
                    abilities.Property(a => a.Wisdom).IsRequired();
                    abilities.Property(a => a.Charisma).IsRequired();
                });

                // Configuration pour le Wallet (owned entity — monnaies D&D 5e)
                entity.OwnsOne(e => e.Wallet, wallet =>
                {
                    wallet.Property(w => w.CopperPieces).IsRequired().HasDefaultValue(0);
                    wallet.Property(w => w.SilverPieces).IsRequired().HasDefaultValue(0);
                    wallet.Property(w => w.ElectrumPieces).IsRequired().HasDefaultValue(0);
                    wallet.Property(w => w.GoldPieces).IsRequired().HasDefaultValue(0);
                    wallet.Property(w => w.PlatinumPieces).IsRequired().HasDefaultValue(0);
                });

                // Index pour améliorer les performances de recherche
                entity.HasIndex(e => e.DiscordUserId);
            });

            // Configuration pour Item (catalogue)
            modelBuilder.Entity<Item>(entity =>
            {
                entity.ToTable("Items");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Icon).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Category).IsRequired();
                entity.Property(e => e.ModelUrl).HasMaxLength(500);

                entity.HasData(GetSeedItems());
            });

            // Configuration pour InventoryEntry
            modelBuilder.Entity<InventoryEntry>(entity =>
            {
                entity.ToTable("InventoryEntries");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Quantity).IsRequired();

                entity.HasOne(e => e.Item)
                    .WithMany()
                    .HasForeignKey(e => e.ItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Cascade on Character delete so a removed character takes its bag with it
                // (no orphaned InventoryEntries pointing at a missing CharacterId).
                entity.HasOne<Character.Models.Character>()
                    .WithMany()
                    .HasForeignKey(e => e.CharacterId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.CharacterId);
                // Unique composite prevents duplicate rows for the same (character, item)
                // under concurrent GiveItemAsync calls — the service stacks instead.
                entity.HasIndex(e => new { e.CharacterId, e.ItemId }).IsUnique();
            });
        }

        /// <summary>
        /// Catalogue initial (POC) — 12 objets.
        /// </summary>
        private static Item[] GetSeedItems() => new[]
        {
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000001"),
                Name = "Potion de soin",
                Description = "Une fiole emplie d'un liquide rouge vif qui semble bouillonner doucement, même au repos.",
                Icon = "potion-red",
                Category = ItemCategory.Consumable,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000002"),
                Name = "Potion de mana",
                Description = "Un flacon au liquide bleu nuit parcouru d'éclats scintillants, comme un ciel étoilé en miniature.",
                Icon = "potion-blue",
                Category = ItemCategory.Consumable,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000003"),
                Name = "Torche",
                Description = "Un bâton de bois enduit de résine à son extrémité, prêt à être allumé pour éclairer les ténèbres.",
                Icon = "torch",
                Category = ItemCategory.Tool,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000004"),
                Name = "Ration de voyage",
                Description = "Un paquet soigneusement emballé contenant du pain dur, de la viande séchée et un morceau de fromage.",
                Icon = "bread",
                Category = ItemCategory.Consumable,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000005"),
                Name = "Corde en chanvre",
                Description = "Une longue corde en chanvre tressé, solide et fiable, indispensable pour tout aventurier.",
                Icon = "rope",
                Category = ItemCategory.Tool,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000006"),
                Name = "Dague affûtée",
                Description = "Une lame courte et effilée, parfaite pour les coups rapides ou un lancer précis.",
                Icon = "dagger",
                Category = ItemCategory.Weapon,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000007"),
                Name = "Arc court",
                Description = "Un arc léger en bois d'if, idéal pour la chasse et les escarmouches à distance.",
                Icon = "bow",
                Category = ItemCategory.Weapon,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000008"),
                Name = "Bouclier en acier",
                Description = "Un bouclier rond en acier forgé, marqué par les coups de batailles passées.",
                Icon = "shield",
                Category = ItemCategory.Armor,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-000000000009"),
                Name = "Parchemin de sort",
                Description = "Un vieux parchemin couvert de runes argentées qui luisent faiblement dans l'obscurité.",
                Icon = "scroll",
                Category = ItemCategory.Magic,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-00000000000a"),
                Name = "Coffre au trésor",
                Description = "Un petit coffre en bois cerclé de fer, fermé par un cadenas rouillé. On entend quelque chose tinter à l'intérieur.",
                Icon = "chest",
                Category = ItemCategory.Treasure,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-00000000000b"),
                Name = "Amulette ancienne",
                Description = "Un pendentif en bronze patiné, orné d'un œil gravé qui semble suivre du regard quiconque le porte.",
                Icon = "amulet",
                Category = ItemCategory.Magic,
            },
            new Item
            {
                Id = Guid.Parse("11111111-1111-1111-1111-00000000000c"),
                Name = "Carte au trésor",
                Description = "Un parchemin jauni et usé, marqué d'un grand X rouge. L'encre semble ancienne mais la carte reste lisible.",
                Icon = "map",
                Category = ItemCategory.Treasure,
            },
        };
    }
}
