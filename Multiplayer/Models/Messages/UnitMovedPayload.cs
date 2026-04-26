using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Models.Messages
{

    /// <summary>
    /// Payload pour le déplacement d'une unité
    /// </summary>
    public class UnitMovedPayload
    {
        /// <summary>
        /// ID de l'unité qui se déplace
        /// </summary>
        public string UnitId { get; set; } = string.Empty;

        /// <summary>
        /// Chemin de déplacement (liste de positions)
        /// </summary>
        public List<GridPosition> Path { get; set; } = new();

        /// <summary>
        /// Coût en points d'action
        /// </summary>
        public int ApCost { get; set; }

        /// <summary>
        /// Points d'action restants après le déplacement
        /// </summary>
        public int RemainingAp { get; set; }
    }

    /// <summary>
    /// Position sur la grille de jeu
    /// </summary>
    public class GridPosition
    {
        public int X { get; set; }
        public int Y { get; set; }

        public GridPosition() { }

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
