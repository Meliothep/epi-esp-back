namespace DnDiscordAPI.Games.Character.Models
{
    /// <summary>
    /// Bourse du personnage — monnaies D&D 5e.
    /// Owned entity (stocké dans la table Characters, comme AbilityScores).
    /// </summary>
    public class Wallet
    {
        public int CopperPieces { get; set; }    // PC
        public int SilverPieces { get; set; }    // PA
        public int ElectrumPieces { get; set; }  // PE
        public int GoldPieces { get; set; }      // PO
        public int PlatinumPieces { get; set; }  // PP

        /// <summary>
        /// Valeur totale convertie en pièces de cuivre.
        /// 1 PP = 10 PO = 50 PE = 100 PA = 1000 PC
        /// </summary>
        public int TotalInCopper =>
            CopperPieces
            + (SilverPieces * 10)
            + (ElectrumPieces * 50)
            + (GoldPieces * 100)
            + (PlatinumPieces * 1000);
    }
}
