using System.Text.Json.Serialization;

namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// Complete snapshot data structure containing all campaign-related information.
/// This is serialized to JSON and stored in CampaignSnapshot.DataJson.
/// </summary>
public class SnapshotData
{
    /// <summary>
    /// Schema version for forward/backward compatibility.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    
    /// <summary>
    /// Timestamp when this snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Campaign metadata and settings.
    /// </summary>
    public CampaignData Campaign { get; set; } = new();
    
    /// <summary>
    /// All characters associated with this campaign.
    /// </summary>
    public List<CharacterData> Characters { get; set; } = [];
    
    /// <summary>
    /// Game sessions history.
    /// </summary>
    public List<GameSessionData> Sessions { get; set; } = [];
    
    /// <summary>
    /// Current 3D scene state.
    /// </summary>
    public SceneStateData SceneState { get; set; } = new();
    
    /// <summary>
    /// Custom game settings and configurations.
    /// </summary>
    public GameSettingsData Settings { get; set; } = new();
}

/// <summary>
/// Campaign metadata snapshot.
/// </summary>
public class CampaignData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DungeonMasterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public CampaignStatus Status { get; set; }
    public Dictionary<string, object> CustomSettings { get; set; } = [];
}

/// <summary>
/// Campaign status enumeration.
/// </summary>
public enum CampaignStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Completed = 3,
    Archived = 4
}

/// <summary>
/// Character snapshot data.
/// </summary>
public class CharacterData
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string? Race { get; set; }
    public int Level { get; set; }
    public CharacterStats Stats { get; set; } = new();
    public List<InventoryItem> Inventory { get; set; } = [];
    public List<string> Abilities { get; set; } = [];
    public List<string> Spells { get; set; } = [];
    public string? BackgroundStory { get; set; }
    public string? Notes { get; set; }
    public DateTime JoinedAt { get; set; }
    public CharacterRole Role { get; set; }
}

/// <summary>
/// Character role in the campaign.
/// </summary>
public enum CharacterRole
{
    Player = 0,
    NPC = 1,
    Companion = 2,
    Enemy = 3
}

/// <summary>
/// Character statistics.
/// </summary>
public class CharacterStats
{
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Intelligence { get; set; }
    public int Wisdom { get; set; }
    public int Charisma { get; set; }
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int Initiative { get; set; }
    public int Speed { get; set; }
    public int ExperiencePoints { get; set; }
    public Dictionary<string, int> Skills { get; set; } = [];
    public Dictionary<string, int> SavingThrows { get; set; } = [];
}

/// <summary>
/// Inventory item data.
/// </summary>
public class InventoryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public string? ItemType { get; set; }
    public bool IsEquipped { get; set; }
    public Dictionary<string, object> Properties { get; set; } = [];
}

/// <summary>
/// Game session history data.
/// </summary>
public class GameSessionData
{
    public Guid Id { get; set; }
    public int SessionNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Summary { get; set; }
    public List<string> ParticipantIds { get; set; } = [];
    public List<GameActionData> Actions { get; set; } = [];
    public string? Notes { get; set; }
}

/// <summary>
/// Individual game action recorded during a session.
/// </summary>
public class GameActionData
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object> Data { get; set; } = [];
}

/// <summary>
/// 3D scene state for BabylonJS.
/// </summary>
public class SceneStateData
{
    /// <summary>
    /// Current map/scene identifier.
    /// </summary>
    public Guid? CurrentMapId { get; set; }
    
    /// <summary>
    /// Map metadata.
    /// </summary>
    public MapData? CurrentMap { get; set; }
    
    /// <summary>
    /// Positions of all entities on the scene.
    /// </summary>
    public List<EntityPosition> EntityPositions { get; set; } = [];
    
    /// <summary>
    /// Camera state.
    /// </summary>
    public CameraState Camera { get; set; } = new();
    
    /// <summary>
    /// Lighting configuration.
    /// </summary>
    public LightingState Lighting { get; set; } = new();
    
    /// <summary>
    /// Fog of war state.
    /// </summary>
    public FogOfWarState FogOfWar { get; set; } = new();
    
    /// <summary>
    /// Active visual effects.
    /// </summary>
    public List<VisualEffectData> ActiveEffects { get; set; } = [];
}

/// <summary>
/// Map data for the 3D scene.
/// </summary>
public class MapData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string? BackgroundAssetId { get; set; }
    public List<MapLayerData> Layers { get; set; } = [];
    public Dictionary<string, object> Properties { get; set; } = [];
}

/// <summary>
/// Map layer data.
/// </summary>
public class MapLayerData
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsVisible { get; set; } = true;
    public List<MapTileData> Tiles { get; set; } = [];
}

/// <summary>
/// Individual map tile data.
/// </summary>
public class MapTileData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string? AssetId { get; set; }
    public bool IsWalkable { get; set; } = true;
    public Dictionary<string, object> Properties { get; set; } = [];
}

/// <summary>
/// Entity position on the 3D scene.
/// </summary>
public class EntityPosition
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "character", "npc", "object", etc.
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float RotationY { get; set; }
    public float Scale { get; set; } = 1.0f;
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Camera state for the 3D viewer.
/// </summary>
public class CameraState
{
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }
    public float Zoom { get; set; } = 1.0f;
}

/// <summary>
/// Lighting state for the scene.
/// </summary>
public class LightingState
{
    public float AmbientIntensity { get; set; } = 0.5f;
    public string AmbientColor { get; set; } = "#ffffff";
    public List<LightSourceData> LightSources { get; set; } = [];
}

/// <summary>
/// Individual light source data.
/// </summary>
public class LightSourceData
{
    public string Type { get; set; } = "point"; // "point", "directional", "spot"
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Intensity { get; set; } = 1.0f;
    public string Color { get; set; } = "#ffffff";
    public float? Range { get; set; }
}

/// <summary>
/// Fog of war state.
/// </summary>
public class FogOfWarState
{
    public bool IsEnabled { get; set; }
    public List<RevealedArea> RevealedAreas { get; set; } = [];
}

/// <summary>
/// Revealed area in fog of war.
/// </summary>
public class RevealedArea
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Radius { get; set; }
}

/// <summary>
/// Visual effect data.
/// </summary>
public class VisualEffectData
{
    public Guid Id { get; set; }
    public string EffectType { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = [];
}

/// <summary>
/// Game settings and configurations.
/// </summary>
public class GameSettingsData
{
    /// <summary>
    /// Game rules configuration.
    /// </summary>
    public RulesConfig Rules { get; set; } = new();
    
    /// <summary>
    /// Combat settings.
    /// </summary>
    public CombatSettings Combat { get; set; } = new();
    
    /// <summary>
    /// House rules and custom modifications.
    /// </summary>
    public Dictionary<string, object> HouseRules { get; set; } = [];
    
    /// <summary>
    /// Enabled optional modules/features.
    /// </summary>
    public List<string> EnabledModules { get; set; } = [];
}

/// <summary>
/// Game rules configuration.
/// </summary>
public class RulesConfig
{
    public string RuleSet { get; set; } = "5e"; // D&D edition
    public bool UseEncumbrance { get; set; }
    public bool UseMulticlassing { get; set; } = true;
    public bool UseFeats { get; set; } = true;
    public int StartingLevel { get; set; } = 1;
    public string AbilityScoreMethod { get; set; } = "standard_array";
}

/// <summary>
/// Combat settings.
/// </summary>
public class CombatSettings
{
    public bool UseInitiativeOrder { get; set; } = true;
    public bool UseGridMovement { get; set; } = true;
    public int GridSquareSize { get; set; } = 5; // feet
    public bool UseOptionalFlanking { get; set; }
    public bool UseAutoRoll { get; set; }
    public string CriticalHitRule { get; set; } = "double_dice";
}

