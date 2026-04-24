using System.Text.Json.Serialization;

namespace DnDiscordAPI.Games.Inventory.DTOs
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InventoryChangeAction
    {
        Added,
        Updated,
        Removed,
    }

    public class InventoryChangedEvent
    {
        public Guid CharacterId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InventoryChangeAction Action { get; set; }

        public InventoryEntryDto Entry { get; set; } = new();
    }

    /// <summary>
    /// Flavor event emitted alongside InventoryChanged when a player consumes an
    /// item (potion drunk, scroll read, torch lit). Drives narration + SFX; the
    /// actual state change goes through InventoryChanged.
    /// </summary>
    public class InventoryItemUsedEvent
    {
        public Guid CharacterId { get; set; }
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
