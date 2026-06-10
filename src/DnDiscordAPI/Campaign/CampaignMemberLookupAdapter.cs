using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Multiplayer.Services;

namespace DnDiscordAPI.Campaign;

/// <summary>
/// Adapter exposant les membres d'une campagne au hub multiplayer
/// (même pattern que <see cref="CampaignMapLookupAdapter"/>).
/// </summary>
public class CampaignMemberLookupAdapter : ICampaignMemberLookupService
{
    private readonly CampaignDbContext _db;

    public CampaignMemberLookupAdapter(CampaignDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveMemberUserIdsAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => new
            {
                c.DungeonMasterId,
                MemberIds = c.Members
                    .Where(m => m.Status == MembershipStatus.Active)
                    .Select(m => m.UserId)
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (campaign is null) return Array.Empty<Guid>();
        return campaign.MemberIds.Append(campaign.DungeonMasterId).Distinct().ToList();
    }

    public async Task<bool> IsMemberAsync(Guid campaignId, Guid userId, CancellationToken ct = default)
    {
        return await _db.Campaigns
            .AsNoTracking()
            .AnyAsync(c =>
                c.Id == campaignId &&
                (c.DungeonMasterId == userId ||
                 c.Members.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active)),
                ct);
    }
}
