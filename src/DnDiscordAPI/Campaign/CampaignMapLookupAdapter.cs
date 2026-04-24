using DnDiscord.Campaign.BL.Maps;
using Multiplayer.Services;

namespace DnDiscordAPI.Campaign;

/// <summary>
/// Adapter wrapping <see cref="ICampaignMapService"/> so the multiplayer hub can look
/// up a map without depending on the Campaign module directly. Mirrors the
/// <see cref="IInventoryGrantService"/> / InventoryGrantAdapter pattern. Lives in
/// DnDiscordAPI because it bridges two projects that can't see each other directly.
/// </summary>
public class CampaignMapLookupAdapter : ICampaignMapLookupService
{
    private readonly ICampaignMapService _maps;

    public CampaignMapLookupAdapter(ICampaignMapService maps)
    {
        _maps = maps;
    }

    public async Task<CampaignMapLookupResult?> GetMapAsync(Guid campaignId, Guid mapId, CancellationToken ct = default)
    {
        var map = await _maps.GetAsync(campaignId, mapId, ct);
        if (map is null) return null;
        return new CampaignMapLookupResult
        {
            Id = map.Id,
            Name = map.Name,
            Data = map.Data,
        };
    }
}
