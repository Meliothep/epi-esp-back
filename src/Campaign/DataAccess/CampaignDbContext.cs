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

    /// <summary>
    /// Campaign members table.
    /// </summary>
    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCampaign(modelBuilder);
        ConfigureCampaignSnapshot(modelBuilder);
        ConfigureCampaignMember(modelBuilder);
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

            entity.Property(c => c.CampaignTreeDefinition)
                .HasColumnType("json");

            entity.Property(c => c.ImageUrl)
                .HasMaxLength(2000);

            entity.Property(c => c.MaxPlayers)
                .HasDefaultValue(6);

            entity.Property(c => c.InviteCode)
                .HasMaxLength(20);

            // Query filter for soft delete
            entity.HasQueryFilter(c => !c.IsDeleted);

            // Indexes
            entity.HasIndex(c => c.DungeonMasterId);
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.CreatedAt);
            entity.HasIndex(c => c.IsPublic);
            entity.HasIndex(c => c.InviteCode).IsUnique();
            entity.HasIndex(c => c.IsDeleted);
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
                .HasMaxLength(64);

            entity.Property(s => s.DataJson)
                .IsRequired()
                .HasColumnType("jsonb");

            entity.HasOne(s => s.Campaign)
                .WithMany(c => c.Snapshots)
                .HasForeignKey(s => s.CampaignId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.CampaignId);
            entity.HasIndex(s => s.CreatedAt);
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => new { s.CampaignId, s.Version }).IsUnique();
        });
    }

    private static void ConfigureCampaignMember(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignMember>(entity =>
        {
            entity.ToTable("CampaignMembers");
            
            entity.HasKey(m => m.Id);
            
            entity.Property(m => m.Id)
                .ValueGeneratedOnAdd();

            entity.Property(m => m.CampaignId)
                .IsRequired();

            entity.Property(m => m.UserId)
                .IsRequired();

            entity.Property(m => m.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(m => m.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(m => m.Nickname)
                .HasMaxLength(100);

            entity.Property(m => m.Notes)
                .HasMaxLength(2000);

            // Relationship: Campaign 1-N Members
            entity.HasOne(m => m.Campaign)
                .WithMany(c => c.Members)
                .HasForeignKey(m => m.CampaignId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(m => m.CampaignId);
            entity.HasIndex(m => m.UserId);
            entity.HasIndex(m => m.Status);
            entity.HasIndex(m => new { m.CampaignId, m.UserId }).IsUnique();
        });
    }
}
