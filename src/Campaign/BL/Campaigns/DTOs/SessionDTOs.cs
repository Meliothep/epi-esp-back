using System.ComponentModel.DataAnnotations;
using DnDiscord.Campaign.DataAccess.Models;

namespace DnDiscord.Campaign.BL.Campaigns.DTOs;

// ─── Requests ────────────────────────────────────────────────────────────────

/// <summary>Request to record navigation to a new node during an active session.</summary>
public class AdvanceSessionRequest
{
    [Required]
    [StringLength(200)]
    public string NodeId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string NodeType { get; set; } = string.Empty;

    [StringLength(200)]
    public string? NodeTitle { get; set; }

    /// <summary>Port used on the previous node to reach this node (e.g. "output", "choice-0").</summary>
    [StringLength(50)]
    public string? PortUsed { get; set; }

    /// <summary>Text of the choice made, when the previous node was a choices node.</summary>
    [StringLength(1000)]
    public string? ChoiceText { get; set; }
}

// ─── Responses ────────────────────────────────────────────────────────────────

public class SessionHistoryEntryResponse
{
    public Guid Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string NodeTitle { get; set; } = string.Empty;
    public string? PortUsed { get; set; }
    public string? ChoiceText { get; set; }
    public DateTime VisitedAt { get; set; }
}

public class GameSessionResponse
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public GameSessionStatus Status { get; set; }
    public string? CurrentNodeId { get; set; }
    public Guid StartedBy { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<SessionHistoryEntryResponse> Entries { get; set; } = [];
}

public class GameSessionListResponse
{
    public List<GameSessionResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
