using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DnDiscord.Campaign.DataAccess;

/// <summary>
/// Entity Framework DbContext for the Campaign module.
/// </summary>
public class CampaignDbContext : DbContext
{
    public CampaignDbContext(DbContextOptions<CampaignDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Campaigns table.
    /// </summary>
    public DbSet<Models.Campaign> Campaigns => Set<Models.Campaign>();

    /// <summary>
    /// Campaign snapshots table for backup/restore functionality.
    /// </summary>
    public DbSet<CampaignSnapshot> CampaignSnapshots => Set<CampaignSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCampaign(modelBuilder);
        ConfigureCampaignSnapshot(modelBuilder);
    }

    private static void ConfigureCampaign(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.Campaign>(entity =>
        {
            entity.ToTable("Campaigns");
            
            entity.HasKey(c => c.Id);
            
            entity.Property(c => c.Id)
                .ValueGeneratedOnAdd();

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.Description)
                .HasMaxLength(4000);

            entity.Property(c => c.DungeonMasterId)
                .IsRequired();

            entity.Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(c => c.SettingsJson)
                .HasColumnType("jsonb");

            entity.HasIndex(c => c.DungeonMasterId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.CreatedAt);
        });
    }

    private static void ConfigureCampaignSnapshot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignSnapshot>(entity =>
        {
            entity.ToTable("CampaignSnapshots");
            
            entity.HasKey(s => s.Id);
            
            entity.Property(s => s.Id)
                .ValueGeneratedOnAdd();

            entity.Property(s => s.CampaignId)
                .IsRequired();

            entity.Property(s => s.Version)
                .IsRequired();

            entity.Property(s => s.Label)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.Description)
                .HasMaxLength(2000);

            entity.Property(s => s.CreatedBy)
                .IsRequired();

            entity.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(s => s.DataHash)
                .IsRequired()
                .HasMaxLength(64); // SHA-256 hex string

            // Store snapshot data as JSONB for better PostgreSQL performance
            entity.Property(s => s.DataJson)
                .IsRequired()
                .HasColumnType("jsonb");

            // Relationship: Campaign 1-N Snapshots
            entity.HasOne(s => s.Campaign)
                .WithMany(c => c.Snapshots)
                .HasForeignKey(s => s.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for common queries
            entity.HasIndex(s => s.CampaignId);
            entity.HasIndex(s => s.CreatedAt);
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => new { s.CampaignId, s.Version }).IsUnique();
        });
    }
}

