namespace Multiplayer.Services;

/// <summary>
/// Minimal read-only lookup exposed to the multiplayer hub so it can resolve a map's
/// payload when the DM triggers a scene switch, without taking a direct dependency on
/// DnDiscord.Campaign (which would be a circular reference). The Campaign module
/// registers an adapter implementation.
/// </summary>
public interface ICampaignMapLookupService
{
    Task<CampaignMapLookupResult?> GetMapAsync(Guid campaignId, Guid mapId, CancellationToken ct = default);
}

public class CampaignMapLookupResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}
