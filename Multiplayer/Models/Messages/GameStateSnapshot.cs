using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Models.Messages;

/// <summary>
/// Snapshot complet de l'état du jeu (pour synchronisation initiale/reconnexion)
/// </summary>
public class GameStateSnapshot
{
    /// <summary>
    /// ID de la session
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// État du combat actuel
    /// </summary>
    public CombatState? CombatState { get; set; }

    /// <summary>
    /// Liste de toutes les unités
    /// </summary>
    public List<UnitState> Units { get; set; } = new();

    /// <summary>
    /// État de la grille/carte
    /// </summary>
    public MapState MapState { get; set; } = new();

    /// <summary>
    /// Dernier numéro de séquence connu
    /// </summary>
    public long LastSequenceNumber { get; set; }
}

/// <summary>
/// État du combat
/// </summary>
public class CombatState
{
    public bool IsActive { get; set; }
    public int CurrentRound { get; set; }
    public string CurrentUnitId { get; set; } = string.Empty;
    public List<InitiativeEntry> InitiativeOrder { get; set; } = new();
}

/// <summary>
/// État d'une unité
/// </summary>
public class UnitState
{
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public GridPosition Position { get; set; } = new();
    public Guid ControllerId { get; set; }
    public List<string> StatusEffects { get; set; } = new();
}

/// <summary>
/// État de la carte
/// </summary>
public class MapState
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<TileInfo> Tiles { get; set; } = new();
}

/// <summary>
/// Informations sur une tuile
/// </summary>
public class TileInfo
{
    public GridPosition Position { get; set; } = new();
    public string Type { get; set; } = string.Empty; // "Floor", "Wall", "Obstacle"
    public bool IsWalkable { get; set; }
}