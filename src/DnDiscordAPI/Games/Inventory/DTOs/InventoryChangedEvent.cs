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
}
