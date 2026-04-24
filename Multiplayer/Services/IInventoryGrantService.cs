namespace Multiplayer.Services;

/// <summary>
/// Minimal surface exposed to the multiplayer hub so it can persist DM-granted items
/// without referencing DnDiscordAPI (which would be a circular dependency).
/// Implemented in DnDiscordAPI as an adapter over IInventoryService.
/// </summary>
public interface IInventoryGrantService
{
    Task<InventoryGrantResult> GrantItemAsync(Guid characterId, Guid itemId, int quantity, Guid campaignId);
}

public class InventoryGrantResult
{
    /// <summary>Display name of the granted item — used for the toast.</summary>
    public string ItemName { get; set; } = string.Empty;
}
