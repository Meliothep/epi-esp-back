namespace DnDiscordAPI.Games.Inventory.Models
{
    /// <summary>
    /// Entrée d'inventaire (lien plat entre un personnage et un objet, avec une quantité).
    /// </summary>
    public class InventoryEntry
    {
        public Guid Id { get; set; }
        public Guid CharacterId { get; set; }
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }

        // Navigation vers l'objet du catalogue (pour hydrater les DTO en une requête).
        public Item? Item { get; set; }
    }
}
