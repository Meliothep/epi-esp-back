using DnDiscord.Campaign.BL.Campaigns.DTOs;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.BL.Sessions;

public interface ICampaignSessionService
{
    Task<GameSessionResponse> CreateSessionAsync(Guid campaignId, Guid userId, CancellationToken ct = default);
    Task<GameSessionResponse?> GetSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
    Task<GameSessionListResponse> ListSessionsAsync(Guid campaignId, Guid userId, CancellationToken ct = default);
    Task<GameSessionResponse?> AdvanceSessionAsync(Guid sessionId, AdvanceSessionRequest request, Guid userId, CancellationToken ct = default);
    Task<GameSessionResponse?> CompleteSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
}

// TODO: Add campaign membership/DM verification in all session operations.
// Currently userId is passed but only used for logging -- any authenticated user
// can manipulate any campaign's sessions. [Authorize] on the controller prevents
// unauthenticated access, but per-campaign authorization is missing.
public class CampaignSessionService : ICampaignSessionService
{
    private readonly CampaignDbContext _db;
    private readonly ILogger<CampaignSessionService> _logger;

    public CampaignSessionService(CampaignDbContext db, ILogger<CampaignSessionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<bool> IsCampaignMemberAsync(Guid campaignId, Guid userId, CancellationToken ct)
    {
        return await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == campaignId &&
                (c.DungeonMasterId == userId ||
                 c.Members.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active)),
                ct);
    }

    public async Task<GameSessionResponse> CreateSessionAsync(Guid campaignId, Guid userId, CancellationToken ct = default)
    {
        // Verify campaign exists and user is a member or DM
        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct)
            ?? throw new InvalidOperationException($"Campaign {campaignId} not found.");

        if (!await IsCampaignMemberAsync(campaignId, userId, ct))
            throw new InvalidOperationException("You don't have permission to create sessions for this campaign.");

        var existing = await _db.GameSessions
            .Include(s => s.Entries.OrderBy(e => e.VisitedAt))
            .Where(s => s.CampaignId == campaignId && s.Status == GameSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (existing != null)
        {
            _logger.LogInformation(
                "CreateSession({CampaignId}) by user {UserId}: reusing existing active session {SessionId}",
                campaignId, userId, existing.Id);
            return MapToResponse(existing);
        }

        var session = new CampaignGameSession
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            Status = GameSessionStatus.Active,
            StartedBy = userId,
            StartedAt = DateTime.UtcNow,
        };

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Session {SessionId} created for campaign {CampaignId} by user {UserId}",
            session.Id, campaignId, userId);

        return MapToResponse(session);
    }

    public async Task<GameSessionResponse?> GetSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var session = await _db.GameSessions
            .Include(s => s.Entries.OrderBy(e => e.VisitedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session != null && !await IsCampaignMemberAsync(session.CampaignId, userId, ct))
            return null;

        return session is null ? null : MapToResponse(session);
    }

    public async Task<GameSessionListResponse> ListSessionsAsync(Guid campaignId, Guid userId, CancellationToken ct = default)
    {
        if (!await IsCampaignMemberAsync(campaignId, userId, ct))
            return new GameSessionListResponse();

        var sessions = await _db.GameSessions
            .Include(s => s.Entries)
            .Where(s => s.CampaignId == campaignId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(ct);

        return new GameSessionListResponse
        {
            Items = sessions.Select(MapToResponse).ToList(),
            TotalCount = sessions.Count,
        };
    }

    public async Task<GameSessionResponse?> AdvanceSessionAsync(Guid sessionId, AdvanceSessionRequest request, Guid userId, CancellationToken ct = default)
    {
        var session = await _db.GameSessions
            .Include(s => s.Entries.OrderBy(e => e.VisitedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return null;
        if (!await IsCampaignMemberAsync(session.CampaignId, userId, ct)) return null;
        if (session.Status != GameSessionStatus.Active)
            throw new InvalidOperationException("Cannot advance a session that is not active.");

        // Record history entry for the node being visited
        var entry = new SessionHistoryEntry
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            NodeId = request.NodeId,
            NodeType = request.NodeType,
            NodeTitle = request.NodeTitle ?? string.Empty,
            PortUsed = request.PortUsed,
            ChoiceText = request.ChoiceText,
            VisitedAt = DateTime.UtcNow,
        };

        session.CurrentNodeId = request.NodeId;
        _db.SessionHistoryEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        return MapToResponse(session);
    }

    public async Task<GameSessionResponse?> CompleteSessionAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var session = await _db.GameSessions
            .Include(s => s.Entries.OrderBy(e => e.VisitedAt))
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null) return null;
        if (!await IsCampaignMemberAsync(session.CampaignId, userId, ct)) return null;

        session.Status = GameSessionStatus.Completed;
        session.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Session {SessionId} completed by user {UserId}", sessionId, userId);
        return MapToResponse(session);
    }

    private static GameSessionResponse MapToResponse(CampaignGameSession session) => new()
    {
        Id = session.Id,
        CampaignId = session.CampaignId,
        Status = session.Status,
        CurrentNodeId = session.CurrentNodeId,
        StartedBy = session.StartedBy,
        StartedAt = session.StartedAt,
        EndedAt = session.EndedAt,
        Entries = session.Entries
            .OrderBy(e => e.VisitedAt)
            .Select(e => new SessionHistoryEntryResponse
            {
                Id = e.Id,
                NodeId = e.NodeId,
                NodeType = e.NodeType,
                NodeTitle = e.NodeTitle,
                PortUsed = e.PortUsed,
                ChoiceText = e.ChoiceText,
                VisitedAt = e.VisitedAt,
            })
            .ToList(),
    };
}
