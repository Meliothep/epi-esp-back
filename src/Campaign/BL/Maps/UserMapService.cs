using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DnDiscord.Campaign.BL.Maps;

/// <summary>
/// Standalone map operations scoped to a single owner.
/// No campaign context required — the caller provides their userId as <c>ownerId</c>.
/// </summary>
public class UserMapService : IUserMapService
{
    private readonly CampaignDbContext _db;

    public UserMapService(CampaignDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CampaignMapDto>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        var rows = await _db.Maps
            .AsNoTracking()
            .Where(m => m.OwnerId == ownerId)
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync(ct);
        return rows.Select(CampaignMapService.ToDto).ToList();
    }

    public async Task<CampaignMapDto?> GetByOwnerAsync(Guid ownerId, Guid mapId, CancellationToken ct = default)
    {
        var row = await _db.Maps
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OwnerId == ownerId && m.Id == mapId, ct);
        return row is null ? null : CampaignMapService.ToDto(row);
    }

    public async Task<CampaignMapDto> CreateAsync(Guid ownerId, CreateUserMapRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var map = new CampaignMap
        {
            Id = Guid.NewGuid(),
            CampaignId = null,
            OwnerId = ownerId,
            IsPublic = false,
            Name = request.Name.Trim(),
            Data = request.Data,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.Maps.Add(map);
        await _db.SaveChangesAsync(ct);
        return CampaignMapService.ToDto(map);
    }

    public async Task<CampaignMapDto?> UpdateAsync(Guid ownerId, Guid mapId, UpdateUserMapRequest request, CancellationToken ct = default)
    {
        var map = await _db.Maps
            .FirstOrDefaultAsync(m => m.OwnerId == ownerId && m.Id == mapId, ct);
        if (map is null) return null;

        if (!string.IsNullOrWhiteSpace(request.Name)) map.Name = request.Name.Trim();
        if (request.Data is not null) map.Data = request.Data;
        if (request.IsPublic.HasValue) map.IsPublic = request.IsPublic.Value;
        map.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return CampaignMapService.ToDto(map);
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid mapId, CancellationToken ct = default)
    {
        var map = await _db.Maps
            .FirstOrDefaultAsync(m => m.OwnerId == ownerId && m.Id == mapId, ct);
        if (map is null) return false;

        _db.Maps.Remove(map);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
