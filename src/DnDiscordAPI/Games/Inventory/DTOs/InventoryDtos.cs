using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using DnDiscordAPI.Games.Inventory.Models;

namespace DnDiscordAPI.Games.Inventory.DTOs
{
    public class ItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemCategory Category { get; set; }

        public string? ModelUrl { get; set; }
    }

    public class InventoryEntryDto
    {
        public Guid Id { get; set; }
        public Guid CharacterId { get; set; }
        public int Quantity { get; set; }
        public ItemDto Item { get; set; } = new();
    }

    public class GiveItemRequest
    {
        [Required]
        public Guid ItemId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Campaign the DM is acting within. Required so the API can verify the caller
        /// is the DM of that campaign before granting the item.
        /// </summary>
        [Required]
        public Guid CampaignId { get; set; }
    }
}
