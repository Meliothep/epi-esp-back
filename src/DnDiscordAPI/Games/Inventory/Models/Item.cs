namespace DnDiscordAPI.Games.Inventory.Models
{
    /// <summary>
    /// Catégories d'objets du catalogue (POC — utilisé pour le visuel côté front).
    /// </summary>
    public enum ItemCategory
    {
        Consumable = 0,
        Weapon = 1,
        Armor = 2,
        Tool = 3,
        Magic = 4,
        Treasure = 5,
    }

    /// <summary>
    /// Objet du catalogue (défini une fois, partagé par toutes les entrées d'inventaire).
    /// </summary>
    public class Item
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Clé d'icône utilisée côté front pour choisir l'emoji/visuel (ex: "potion-red").
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Catégorie — sert au front pour styliser la carte (couleur, gradient).
        /// </summary>
        public ItemCategory Category { get; set; }

        /// <summary>
        /// URL d'un modèle 3D poly.pizza (optionnel, pour affichage 3D futur).
        /// </summary>
        public string? ModelUrl { get; set; }

        /// <summary>
        /// Prix en pièces d'or (GP). 0 = non vendable en boutique.
        /// </summary>
        public int GoldCost { get; set; }
    }
}
