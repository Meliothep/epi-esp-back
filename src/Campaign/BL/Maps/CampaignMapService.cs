using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DnDiscord.Campaign.BL.Maps;

public class CampaignMapService : ICampaignMapService
{
    private readonly CampaignDbContext _db;

    public CampaignMapService(CampaignDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CampaignMapDto>> ListAsync(Guid campaignId, CancellationToken ct = default)
    {
        var rows = await _db.Maps
            .AsNoTracking()
            .Where(m => m.CampaignId == campaignId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<CampaignMapDto?> GetAsync(Guid campaignId, Guid mapId, CancellationToken ct = default)
    {
        var row = await _db.Maps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.Id == mapId, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<CampaignMapDto> CreateAsync(Guid campaignId, Guid ownerId, CreateCampaignMapRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var map = new CampaignMap
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            OwnerId = ownerId, // requis pour que PUT /api/maps/mine/:id fonctionne
            Name = request.Name.Trim(),
            Data = request.Data,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Maps.Add(map);
        await _db.SaveChangesAsync(ct);
        return ToDto(map);
    }

    public async Task<CampaignMapDto?> UpdateAsync(Guid campaignId, Guid mapId, UpdateCampaignMapRequest request, CancellationToken ct = default)
    {
        var map = await _db.Maps
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.Id == mapId, ct);
        if (map is null) return null;

        if (!string.IsNullOrWhiteSpace(request.Name)) map.Name = request.Name.Trim();
        if (request.Data is not null) map.Data = request.Data;
        if (request.IsPublic.HasValue) map.IsPublic = request.IsPublic.Value;
        map.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(map);
    }

    public async Task<bool> DeleteAsync(Guid campaignId, Guid mapId, CancellationToken ct = default)
    {
        var map = await _db.Maps
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.Id == mapId, ct);
        if (map is null) return false;

        _db.Maps.Remove(map);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CampaignMapDto?> GetByMapIdAsync(Guid campaignId, Guid mapId, CancellationToken ct = default)
    {
        var row = await _db.Maps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.Id == mapId, ct);
        return row is null ? null : ToDto(row);
    }

    internal static CampaignMapDto ToDto(CampaignMap m) =>
        new(m.Id, m.CampaignId, m.OwnerId, m.IsPublic, m.Name, m.Data, m.CreatedAt, m.UpdatedAt);
}
