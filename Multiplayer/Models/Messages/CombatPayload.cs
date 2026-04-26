using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static Multiplayer.Define;

namespace Multiplayer.Models.Messages;

/// <summary>
/// Payload pour le début d'un combat
/// </summary>
public class CombatStartedPayload
{
    /// <summary>
    /// Ordre d'initiative (liste d'IDs d'unités)
    /// </summary>
    public List<InitiativeEntry> InitiativeOrder { get; set; } = new();

    /// <summary>
    /// Liste des ennemis
    /// </summary>
    public List<EnemyInfo> Enemies { get; set; } = new();

    /// <summary>
    /// Server-authoritative combat state. Clients read these fields verbatim —
    /// no local initiative computation. Added with the hub-authoritative rework.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CombatPhase Phase { get; set; } = CombatPhase.PlayerTurn;
    public int Round { get; set; } = 1;
    public string? CurrentUnitId { get; set; }
    public List<string> TurnOrder { get; set; } = new();
    public List<Models.UnitRuntimeState> Units { get; set; } = new();
}

/// <summary>
/// Entrée dans l'ordre d'initiative
/// </summary>
public class InitiativeEntry
{
    public string UnitId { get; set; } = string.Empty;
    public int Initiative { get; set; }
    public Guid ControllerId { get; set; } // PlayerId qui contrôle l'unité
}

/// <summary>
/// Informations sur un ennemi
/// </summary>
public class EnemyInfo
{
    public string EnemyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public GridPosition Position { get; set; } = new();
}

/// <summary>
/// Payload pour la fin d'un combat
/// </summary>
public class CombatEndedPayload
{
    /// <summary>
    /// Résultat du combat
    /// </summary>
    public CombatResult Result { get; set; }

    /// <summary>
    /// Récompenses obtenues
    /// </summary>
    public CombatRewards Rewards { get; set; } = new();
}

/// <summary>
/// Récompenses de combat
/// </summary>
public class CombatRewards
{
    public int ExperienceGained { get; set; }
    public int GoldGained { get; set; }
    public List<string> ItemsObtained { get; set; } = new();
}