namespace DnDiscord.Campaign.DataAccess.Models;

public class SessionHistoryEntry
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string NodeTitle { get; set; } = string.Empty;
    public string? PortUsed { get; set; }
    public string? ChoiceText { get; set; }
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CampaignGameSession Session { get; set; } = null!;
}
