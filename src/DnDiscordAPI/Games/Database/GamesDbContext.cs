using DnDiscordAPI.Games.Character.Models;
using Microsoft.EntityFrameworkCore;

namespace DnDiscordAPI.Games.Database
{
    public class GamesDbContext : DbContext
    {
        public GamesDbContext(DbContextOptions<GamesDbContext> options) : base(options)
        {
        }

        public DbSet<Character.Models.Character> Characters { get; set; }

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

                // Index pour améliorer les performances de recherche
                entity.HasIndex(e => e.DiscordUserId);
            });
        }
    }
}

