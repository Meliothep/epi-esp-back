namespace DnDiscord.Campaign.BL.Maps;

/// <summary>
/// Campaign-scoped map operations (DM-only writes, member reads).
/// </summary>
public interface ICampaignMapService
{
    Task<IReadOnlyList<CampaignMapDto>> ListAsync(Guid campaignId, CancellationToken ct = default);
    Task<CampaignMapDto?> GetAsync(Guid campaignId, Guid mapId, CancellationToken ct = default);
    /// <param name="ownerId">UserId du créateur (DM). Obligatoire pour que PUT /api/maps/mine/:id fonctionne ensuite.</param>
    Task<CampaignMapDto> CreateAsync(Guid campaignId, Guid ownerId, CreateCampaignMapRequest request, CancellationToken ct = default);
    Task<CampaignMapDto?> UpdateAsync(Guid campaignId, Guid mapId, UpdateCampaignMapRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid campaignId, Guid mapId, CancellationToken ct = default);

    /// <summary>
    /// Récupère une map par son ID en vérifiant qu'elle appartient bien à la campagne donnée.
    /// Utilisé exclusivement par la route session-map pour les clients en session
    /// qui n'ont pas la map en cache et ne sont pas owners.
    /// </summary>
    Task<CampaignMapDto?> GetByMapIdAsync(Guid campaignId, Guid mapId, CancellationToken ct = default);
}

/// <summary>
/// User-scoped standalone map operations (owner-only, no campaign required).
/// </summary>
public interface IUserMapService
{
    Task<IReadOnlyList<CampaignMapDto>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default);
    Task<CampaignMapDto?> GetByOwnerAsync(Guid ownerId, Guid mapId, CancellationToken ct = default);
    Task<CampaignMapDto> CreateAsync(Guid ownerId, CreateUserMapRequest request, CancellationToken ct = default);
    Task<CampaignMapDto?> UpdateAsync(Guid ownerId, Guid mapId, UpdateUserMapRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid ownerId, Guid mapId, CancellationToken ct = default);
}
