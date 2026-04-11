namespace DnDiscord.Campaign.DataAccess.Models;

public enum GameSessionStatus
{
    Active = 0,
    Completed = 1,
    Abandoned = 2,
}

public class CampaignGameSession
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public GameSessionStatus Status { get; set; } = GameSessionStatus.Active;
    public string? CurrentNodeId { get; set; }
    public Guid StartedBy { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    // Navigation
    public Campaign Campaign { get; set; } = null!;
    public ICollection<SessionHistoryEntry> Entries { get; set; } = [];
}
