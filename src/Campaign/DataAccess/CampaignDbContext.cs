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

    /// <summary>
    /// Campaign game sessions table.
    /// </summary>
    public DbSet<CampaignGameSession> GameSessions => Set<CampaignGameSession>();

    /// <summary>
    /// Session history entries table.
    /// </summary>
    public DbSet<SessionHistoryEntry> SessionHistoryEntries => Set<SessionHistoryEntry>();

    /// <summary>
    /// Maps persisted for each campaign — the DM picks from these to start/switch
    /// the scene during a session.
    /// </summary>
    public DbSet<CampaignMap> Maps => Set<CampaignMap>();

    /// <summary>
    /// Successful dice roll submissions persisted from <c>GameHub.SubmitRollResult</c>
    /// for the campaign journal.
    /// </summary>
    public DbSet<RollHistoryEntry> RollHistory => Set<RollHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCampaign(modelBuilder);
        ConfigureCampaignSnapshot(modelBuilder);
        ConfigureCampaignMember(modelBuilder);
        ConfigureCampaignGameSession(modelBuilder);
        ConfigureSessionHistoryEntry(modelBuilder);
        ConfigureCampaignMap(modelBuilder);
        ConfigureRollHistoryEntry(modelBuilder);
    }

    private static void ConfigureCampaignMap(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignMap>(entity =>
        {
            entity.ToTable("CampaignMaps");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedOnAdd();

            entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Data).IsRequired().HasColumnType("jsonb");

            entity.HasOne<Models.Campaign>()
                .WithMany()
                .HasForeignKey(m => m.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(m => m.CampaignId);
            entity.HasIndex(m => new { m.CampaignId, m.CreatedAt });
        });
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

            entity.Property(c => c.ImageUrl)
                .HasMaxLength(2000);

            entity.Property(c => c.CampaignTreeDefinition)
                .HasColumnType("json");

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
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(m => m.CampaignId);
            entity.HasIndex(m => m.UserId);
            entity.HasIndex(m => m.Status);
            entity.HasIndex(m => new { m.CampaignId, m.UserId }).IsUnique();
        });
    }

    private static void ConfigureCampaignGameSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignGameSession>(entity =>
        {
            entity.ToTable("CampaignGameSessions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.Property(s => s.CampaignId).IsRequired();
            entity.Property(s => s.StartedBy).IsRequired();
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(s => s.CurrentNodeId).HasMaxLength(200);
            entity.HasOne(s => s.Campaign)
                .WithMany(c => c.GameSessions)
                .HasForeignKey(s => s.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => s.CampaignId);
            entity.HasIndex(s => s.StartedBy);
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.StartedAt);
        });
    }

    private static void ConfigureRollHistoryEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RollHistoryEntry>(entity =>
        {
            entity.ToTable("RollHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.CampaignId).IsRequired();
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RequestId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.UserName).HasMaxLength(200);
            entity.Property(e => e.DiceType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Label).HasMaxLength(200);

            // Journal queries: list a campaign's rolls newest-first. PG defaults
            // index sort order to ASC; range scans on RolledAt with ORDER BY DESC
            // still use the index efficiently for POC scope.
            entity.HasIndex(e => new { e.CampaignId, e.RolledAt })
                  .HasDatabaseName("ix_rollhistory_campaign_rolledat");

            // Session-scoped views (e.g. recap of the latest game).
            entity.HasIndex(e => e.SessionId)
                  .HasDatabaseName("ix_rollhistory_session");
        });
    }

    private static void ConfigureSessionHistoryEntry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionHistoryEntry>(entity =>
        {
            entity.ToTable("SessionHistoryEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NodeType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.NodeTitle).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PortUsed).HasMaxLength(50);
            entity.Property(e => e.ChoiceText).HasMaxLength(1000);
            entity.HasOne(e => e.Session)
                .WithMany(s => s.Entries)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.VisitedAt);
        });
    }
}
