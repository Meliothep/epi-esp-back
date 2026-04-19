namespace DnDiscord.Campaign.BL.Maps;

public interface ICampaignMapService
{
    Task<IReadOnlyList<CampaignMapDto>> ListAsync(Guid campaignId, CancellationToken ct = default);
    Task<CampaignMapDto?> GetAsync(Guid campaignId, Guid mapId, CancellationToken ct = default);
    Task<CampaignMapDto> CreateAsync(Guid campaignId, CreateCampaignMapRequest request, CancellationToken ct = default);
    Task<CampaignMapDto?> UpdateAsync(Guid campaignId, Guid mapId, UpdateCampaignMapRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid campaignId, Guid mapId, CancellationToken ct = default);
}
