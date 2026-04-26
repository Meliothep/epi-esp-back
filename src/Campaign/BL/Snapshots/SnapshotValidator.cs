using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.Extensions.Logging;

namespace DnDiscord.Campaign.BL.Snapshots;

/// <summary>
/// Validates snapshot data integrity and consistency.
/// </summary>
public interface ISnapshotValidator
{
    /// <summary>
    /// Validates snapshot data structure and content.
    /// </summary>
    ValidationResult Validate(SnapshotData data);
    
    /// <summary>
    /// Validates that a snapshot can be restored to the target campaign.
    /// </summary>
    ValidationResult ValidateForRestore(SnapshotData data, Guid targetCampaignId);
    
    /// <summary>
    /// Validates imported JSON data before creating a snapshot.
    /// </summary>
    ValidationResult ValidateImport(SnapshotData data);
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationError> Errors { get; init; } = [];
    public List<ValidationWarning> Warnings { get; init; } = [];
    
    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(params ValidationError[] errors) => new()
    {
        IsValid = false,
        Errors = [..errors]
    };
    
    public static ValidationResult Failure(string code, string message) => new()
    {
        IsValid = false,
        Errors = [new ValidationError(code, message)]
    };
}

/// <summary>
/// Represents a validation error.
/// </summary>
public record ValidationError(string Code, string Message, string? Field = null);

/// <summary>
/// Represents a validation warning (non-blocking).
/// </summary>
public record ValidationWarning(string Code, string Message, string? Field = null);

/// <summary>
/// Implementation of snapshot validator.
/// </summary>
public class SnapshotValidator : ISnapshotValidator
{
    private readonly ILogger<SnapshotValidator> _logger;
    
    // Supported schema versions
    private static readonly int[] SupportedSchemaVersions = [1];
    
    // Validation limits
    private const int MaxCharacters = 100;
    private const int MaxSessions = 1000;
    private const int MaxActionsPerSession = 10000;
    private const int MaxInventoryItemsPerCharacter = 500;
    private const int MaxEntityPositions = 1000;
    
    public SnapshotValidator(ILogger<SnapshotValidator> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public ValidationResult Validate(SnapshotData data)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();
        
        // Schema version check
        if (!SupportedSchemaVersions.Contains(data.SchemaVersion))
        {
            errors.Add(new ValidationError(
                "UNSUPPORTED_SCHEMA",
                $"Schema version {data.SchemaVersion} is not supported. Supported versions: {string.Join(", ", SupportedSchemaVersions)}",
                nameof(data.SchemaVersion)));
        }
        
        // Campaign validation
        ValidateCampaign(data.Campaign, errors, warnings);
        
        // Characters validation
        ValidateCharacters(data.Characters, errors, warnings);
        
        // Sessions validation
        ValidateSessions(data.Sessions, errors, warnings);
        
        // Scene state validation
        ValidateSceneState(data.SceneState, errors, warnings);
        
        // Settings validation
        ValidateSettings(data.Settings, errors, warnings);
        
        if (errors.Count > 0)
        {
            _logger.LogWarning("Snapshot validation failed with {ErrorCount} errors", errors.Count);
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateForRestore(SnapshotData data, Guid targetCampaignId)
    {
        var baseResult = Validate(data);
        var errors = new List<ValidationError>(baseResult.Errors);
        var warnings = new List<ValidationWarning>(baseResult.Warnings);
        
        // Verify campaign ID matches (or is being restored to a different campaign intentionally)
        if (data.Campaign.Id != targetCampaignId)
        {
            warnings.Add(new ValidationWarning(
                "CAMPAIGN_ID_MISMATCH",
                $"Snapshot was created for campaign {data.Campaign.Id} but is being restored to {targetCampaignId}",
                nameof(data.Campaign.Id)));
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateImport(SnapshotData data)
    {
        var baseResult = Validate(data);
        var errors = new List<ValidationError>(baseResult.Errors);
        var warnings = new List<ValidationWarning>(baseResult.Warnings);
        
        // Additional security checks for imported data
        
        // Check for potentially malicious content in text fields
        ValidateTextFieldsSecurity(data, errors);
        
        // Check for reasonable data sizes
        ValidateDataSizes(data, errors, warnings);
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
    
    private static void ValidateCampaign(CampaignData campaign, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (campaign.Id == Guid.Empty)
        {
            errors.Add(new ValidationError("INVALID_CAMPAIGN_ID", "Campaign ID cannot be empty", "Campaign.Id"));
        }
        
        if (string.IsNullOrWhiteSpace(campaign.Name))
        {
            errors.Add(new ValidationError("INVALID_CAMPAIGN_NAME", "Campaign name is required", "Campaign.Name"));
        }
        else if (campaign.Name.Length > 200)
        {
            errors.Add(new ValidationError("CAMPAIGN_NAME_TOO_LONG", "Campaign name exceeds 200 characters", "Campaign.Name"));
        }
        
        if (campaign.DungeonMasterId == Guid.Empty)
        {
            errors.Add(new ValidationError("INVALID_DM_ID", "Dungeon Master ID cannot be empty", "Campaign.DungeonMasterId"));
        }
        
        if (campaign.CreatedAt > DateTime.UtcNow.AddMinutes(5))
        {
            warnings.Add(new ValidationWarning("FUTURE_CREATION_DATE", "Campaign creation date is in the future", "Campaign.CreatedAt"));
        }
    }
    
    private static void ValidateCharacters(List<CharacterData> characters, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (characters.Count > MaxCharacters)
        {
            errors.Add(new ValidationError(
                "TOO_MANY_CHARACTERS",
                $"Snapshot contains {characters.Count} characters, maximum allowed is {MaxCharacters}",
                "Characters"));
        }
        
        var characterIds = new HashSet<Guid>();
        
        foreach (var character in characters)
        {
            if (character.Id == Guid.Empty)
            {
                errors.Add(new ValidationError("INVALID_CHARACTER_ID", "Character ID cannot be empty", "Characters[].Id"));
                continue;
            }
            
            if (!characterIds.Add(character.Id))
            {
                errors.Add(new ValidationError("DUPLICATE_CHARACTER_ID", $"Duplicate character ID: {character.Id}", "Characters[].Id"));
            }
            
            if (string.IsNullOrWhiteSpace(character.Name))
            {
                errors.Add(new ValidationError("INVALID_CHARACTER_NAME", $"Character {character.Id} has no name", "Characters[].Name"));
            }
            
            if (character.Level < 1 || character.Level > 30)
            {
                warnings.Add(new ValidationWarning("UNUSUAL_CHARACTER_LEVEL", $"Character {character.Name} has unusual level: {character.Level}", "Characters[].Level"));
            }
            
            if (character.Inventory.Count > MaxInventoryItemsPerCharacter)
            {
                errors.Add(new ValidationError(
                    "TOO_MANY_INVENTORY_ITEMS",
                    $"Character {character.Name} has {character.Inventory.Count} inventory items, maximum is {MaxInventoryItemsPerCharacter}",
                    "Characters[].Inventory"));
            }
            
            // Validate stats
            ValidateCharacterStats(character, errors, warnings);
        }
    }
    
    private static void ValidateCharacterStats(CharacterData character, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        var stats = character.Stats;
        
        // D&D stats typically range from 1-30
        var statValues = new[] 
        { 
            (Name: "Strength", Value: stats.Strength),
            (Name: "Dexterity", Value: stats.Dexterity),
            (Name: "Constitution", Value: stats.Constitution),
            (Name: "Intelligence", Value: stats.Intelligence),
            (Name: "Wisdom", Value: stats.Wisdom),
            (Name: "Charisma", Value: stats.Charisma)
        };
        
        foreach (var stat in statValues)
        {
            if (stat.Value < 1 || stat.Value > 30)
            {
                warnings.Add(new ValidationWarning(
                    "UNUSUAL_STAT_VALUE",
                    $"Character {character.Name} has unusual {stat.Name}: {stat.Value}",
                    $"Characters[].Stats.{stat.Name}"));
            }
        }
        
        if (stats.HitPoints < 0)
        {
            warnings.Add(new ValidationWarning("NEGATIVE_HP", $"Character {character.Name} has negative HP", "Characters[].Stats.HitPoints"));
        }
        
        if (stats.MaxHitPoints < 1)
        {
            errors.Add(new ValidationError("INVALID_MAX_HP", $"Character {character.Name} has invalid max HP", "Characters[].Stats.MaxHitPoints"));
        }
    }
    
    private static void ValidateSessions(List<GameSessionData> sessions, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (sessions.Count > MaxSessions)
        {
            errors.Add(new ValidationError(
                "TOO_MANY_SESSIONS",
                $"Snapshot contains {sessions.Count} sessions, maximum allowed is {MaxSessions}",
                "Sessions"));
        }
        
        var sessionIds = new HashSet<Guid>();
        
        foreach (var session in sessions)
        {
            if (session.Id == Guid.Empty)
            {
                errors.Add(new ValidationError("INVALID_SESSION_ID", "Session ID cannot be empty", "Sessions[].Id"));
                continue;
            }
            
            if (!sessionIds.Add(session.Id))
            {
                errors.Add(new ValidationError("DUPLICATE_SESSION_ID", $"Duplicate session ID: {session.Id}", "Sessions[].Id"));
            }
            
            if (session.Actions.Count > MaxActionsPerSession)
            {
                warnings.Add(new ValidationWarning(
                    "MANY_SESSION_ACTIONS",
                    $"Session {session.SessionNumber} has {session.Actions.Count} actions",
                    "Sessions[].Actions"));
            }
            
            if (session.EndedAt.HasValue && session.EndedAt < session.StartedAt)
            {
                errors.Add(new ValidationError(
                    "INVALID_SESSION_TIMES",
                    $"Session {session.SessionNumber} end time is before start time",
                    "Sessions[].EndedAt"));
            }
        }
    }
    
    private static void ValidateSceneState(SceneStateData sceneState, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (sceneState.EntityPositions.Count > MaxEntityPositions)
        {
            errors.Add(new ValidationError(
                "TOO_MANY_ENTITIES",
                $"Scene contains {sceneState.EntityPositions.Count} entities, maximum allowed is {MaxEntityPositions}",
                "SceneState.EntityPositions"));
        }
        
        var entityIds = new HashSet<Guid>();
        
        foreach (var position in sceneState.EntityPositions)
        {
            if (position.EntityId == Guid.Empty)
            {
                errors.Add(new ValidationError("INVALID_ENTITY_ID", "Entity ID cannot be empty", "SceneState.EntityPositions[].EntityId"));
                continue;
            }
            
            if (!entityIds.Add(position.EntityId))
            {
                errors.Add(new ValidationError("DUPLICATE_ENTITY_POSITION", $"Duplicate entity position for: {position.EntityId}", "SceneState.EntityPositions[].EntityId"));
            }
            
            if (position.Scale <= 0)
            {
                warnings.Add(new ValidationWarning("INVALID_SCALE", $"Entity {position.EntityId} has non-positive scale", "SceneState.EntityPositions[].Scale"));
            }
        }
        
        // Validate camera
        if (sceneState.Camera.Zoom <= 0)
        {
            warnings.Add(new ValidationWarning("INVALID_CAMERA_ZOOM", "Camera zoom is non-positive", "SceneState.Camera.Zoom"));
        }
        
        // Validate lighting
        if (sceneState.Lighting.AmbientIntensity < 0 || sceneState.Lighting.AmbientIntensity > 1)
        {
            warnings.Add(new ValidationWarning("INVALID_AMBIENT_INTENSITY", "Ambient intensity should be between 0 and 1", "SceneState.Lighting.AmbientIntensity"));
        }
    }
    
    private static void ValidateSettings(GameSettingsData settings, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        if (settings.Combat.GridSquareSize <= 0)
        {
            errors.Add(new ValidationError("INVALID_GRID_SIZE", "Grid square size must be positive", "Settings.Combat.GridSquareSize"));
        }
        
        if (settings.Rules.StartingLevel < 1 || settings.Rules.StartingLevel > 20)
        {
            warnings.Add(new ValidationWarning("UNUSUAL_STARTING_LEVEL", $"Unusual starting level: {settings.Rules.StartingLevel}", "Settings.Rules.StartingLevel"));
        }
    }
    
    private static void ValidateTextFieldsSecurity(SnapshotData data, List<ValidationError> errors)
    {
        // Check for potential script injection in text fields
        var suspiciousPatterns = new[] { "<script", "javascript:", "onerror=", "onload=" };
        
        void CheckField(string? value, string fieldName)
        {
            if (string.IsNullOrEmpty(value)) return;
            
            foreach (var pattern in suspiciousPatterns)
            {
                if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationError(
                        "POTENTIALLY_MALICIOUS_CONTENT",
                        $"Field contains potentially malicious content",
                        fieldName));
                    return;
                }
            }
        }
        
        CheckField(data.Campaign.Name, "Campaign.Name");
        CheckField(data.Campaign.Description, "Campaign.Description");
        
        foreach (var character in data.Characters)
        {
            CheckField(character.Name, "Characters[].Name");
            CheckField(character.BackgroundStory, "Characters[].BackgroundStory");
            CheckField(character.Notes, "Characters[].Notes");
        }
        
        foreach (var session in data.Sessions)
        {
            CheckField(session.Summary, "Sessions[].Summary");
            CheckField(session.Notes, "Sessions[].Notes");
        }
    }
    
    private static void ValidateDataSizes(SnapshotData data, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        // Warn about large descriptions
        if (data.Campaign.Description?.Length > 10000)
        {
            warnings.Add(new ValidationWarning("LARGE_DESCRIPTION", "Campaign description is very large", "Campaign.Description"));
        }
        
        // Count total actions
        var totalActions = data.Sessions.Sum(s => s.Actions.Count);
        if (totalActions > 100000)
        {
            warnings.Add(new ValidationWarning("MANY_TOTAL_ACTIONS", $"Snapshot contains {totalActions} total actions", "Sessions[].Actions"));
        }
    }
}

