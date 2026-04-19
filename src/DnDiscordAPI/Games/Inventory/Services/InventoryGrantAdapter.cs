using DnDiscordAPI.Games.Inventory.DTOs;
using Multiplayer.Services;

namespace DnDiscordAPI.Games.Inventory.Services;

/// <summary>
/// Adapter wrapping <see cref="IInventoryService"/> so the multiplayer hub can persist
/// DM-granted items without taking a direct dependency on DnDiscordAPI.
/// </summary>
public class InventoryGrantAdapter : IInventoryGrantService
{
    private readonly IInventoryService _inventoryService;

    public InventoryGrantAdapter(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<InventoryGrantResult> GrantItemAsync(Guid characterId, Guid itemId, int quantity, Guid campaignId)
    {
        var entry = await _inventoryService.GiveItemAsync(characterId, new GiveItemRequest
        {
            ItemId = itemId,
            Quantity = quantity,
            CampaignId = campaignId,
        });

        return new InventoryGrantResult { ItemName = entry.Item.Name };
    }
}
