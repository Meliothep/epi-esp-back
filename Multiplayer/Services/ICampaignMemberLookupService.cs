namespace Multiplayer.Services;

/// <summary>
/// Lookup lecture seule des membres d'une campagne pour le hub multiplayer.
/// Le module Campaign fournit l'adapter (même pattern que ICampaignMapLookupService).
/// </summary>
public interface ICampaignMemberLookupService
{
    /// <summary>MJ + membres actifs de la campagne.</summary>
    Task<IReadOnlyList<Guid>> GetActiveMemberUserIdsAsync(Guid campaignId, CancellationToken ct = default);

    /// <summary>True si l'utilisateur est MJ ou membre actif de la campagne.</summary>
    Task<bool> IsMemberAsync(Guid campaignId, Guid userId, CancellationToken ct = default);
}
