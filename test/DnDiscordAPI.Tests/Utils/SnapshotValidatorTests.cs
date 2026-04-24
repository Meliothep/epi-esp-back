using DnDiscord.Campaign.BL.Snapshots;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DnDiscordAPI.Tests.Utils;

public sealed class SnapshotValidatorTests
{
    private readonly SnapshotValidator _validator = new(NullLogger<SnapshotValidator>.Instance);

    private static SnapshotData CreateValidSnapshotData()
    {
        var campaignId = Guid.NewGuid();
        var dmId = Guid.NewGuid();

        return new SnapshotData
        {
            SchemaVersion = 1,
            Campaign = new CampaignData
            {
                Id = campaignId,
                Name = "Test Campaign",
                DungeonMasterId = dmId,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            Characters = new List<CharacterData>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Hero",
                    Level = 5,
                    Stats = new CharacterStats
                    {
                        Strength = 16, Dexterity = 14, Constitution = 12,
                        Intelligence = 10, Wisdom = 13, Charisma = 8,
                        HitPoints = 30, MaxHitPoints = 30
                    },
                    Inventory = new List<InventoryItem>()
                }
            },
            Sessions = new List<GameSessionData>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SessionNumber = 1,
                    StartedAt = DateTime.UtcNow.AddHours(-3),
                    Actions = new List<GameActionData>()
                }
            },
            SceneState = new SceneStateData
            {
                EntityPositions = new List<EntityPosition>(),
                Camera = new CameraState { Zoom = 1.0f },
                Lighting = new LightingState { AmbientIntensity = 0.5f }
            },
            Settings = new GameSettingsData
            {
                Combat = new CombatSettings { GridSquareSize = 5 },
                Rules = new RulesConfig { StartingLevel = 1 }
            }
        };
    }

    // --- Validate basic ---

    [Fact]
    public void Validate_ValidData_ReturnsSuccess()
    {
        var data = CreateValidSnapshotData();
        var result = _validator.Validate(data);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.SchemaVersion = 999;
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "UNSUPPORTED_SCHEMA");
    }

    [Fact]
    public void Validate_EmptyCampaignId_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.Id = Guid.Empty;
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_CAMPAIGN_ID");
    }

    [Fact]
    public void Validate_EmptyCampaignName_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.Name = "";
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_CAMPAIGN_NAME");
    }

    [Fact]
    public void Validate_CampaignNameTooLong_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.Name = new string('A', 201);
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "CAMPAIGN_NAME_TOO_LONG");
    }

    [Fact]
    public void Validate_EmptyDungeonMasterId_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.DungeonMasterId = Guid.Empty;
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_DM_ID");
    }

    [Fact]
    public void Validate_FutureCreationDate_AddsWarning()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.CreatedAt = DateTime.UtcNow.AddHours(1);
        var result = _validator.Validate(data);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "FUTURE_CREATION_DATE");
    }

    // --- Character validation ---

    [Fact]
    public void Validate_DuplicateCharacterId_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        var id = Guid.NewGuid();
        data.Characters = new List<CharacterData>
        {
            new() { Id = id, Name = "Char1", Level = 1, Stats = ValidStats(), Inventory = new() },
            new() { Id = id, Name = "Char2", Level = 1, Stats = ValidStats(), Inventory = new() }
        };
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "DUPLICATE_CHARACTER_ID");
    }

    [Fact]
    public void Validate_EmptyCharacterName_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Characters[0].Name = "";
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_CHARACTER_NAME");
    }

    [Fact]
    public void Validate_InvalidMaxHitPoints_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Characters[0].Stats.MaxHitPoints = 0;
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_MAX_HP");
    }

    [Fact]
    public void Validate_UnusualCharacterLevel_AddsWarning()
    {
        var data = CreateValidSnapshotData();
        data.Characters[0].Level = 0;
        var result = _validator.Validate(data);
        Assert.Contains(result.Warnings, w => w.Code == "UNUSUAL_CHARACTER_LEVEL");
    }

    // --- Session validation ---

    [Fact]
    public void Validate_SessionEndBeforeStart_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Sessions[0].EndedAt = data.Sessions[0].StartedAt.AddHours(-1);
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_SESSION_TIMES");
    }

    [Fact]
    public void Validate_DuplicateSessionId_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        var id = Guid.NewGuid();
        data.Sessions = new List<GameSessionData>
        {
            new() { Id = id, SessionNumber = 1, StartedAt = DateTime.UtcNow, Actions = new() },
            new() { Id = id, SessionNumber = 2, StartedAt = DateTime.UtcNow, Actions = new() }
        };
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "DUPLICATE_SESSION_ID");
    }

    // --- Scene state validation ---

    [Fact]
    public void Validate_NonPositiveCameraZoom_AddsWarning()
    {
        var data = CreateValidSnapshotData();
        data.SceneState.Camera.Zoom = 0;
        var result = _validator.Validate(data);
        Assert.Contains(result.Warnings, w => w.Code == "INVALID_CAMERA_ZOOM");
    }

    [Fact]
    public void Validate_AmbientIntensityOutOfRange_AddsWarning()
    {
        var data = CreateValidSnapshotData();
        data.SceneState.Lighting.AmbientIntensity = 1.5f;
        var result = _validator.Validate(data);
        Assert.Contains(result.Warnings, w => w.Code == "INVALID_AMBIENT_INTENSITY");
    }

    // --- Settings validation ---

    [Fact]
    public void Validate_InvalidGridSquareSize_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Settings.Combat.GridSquareSize = 0;
        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_GRID_SIZE");
    }

    [Fact]
    public void Validate_UnusualStartingLevel_AddsWarning()
    {
        var data = CreateValidSnapshotData();
        data.Settings.Rules.StartingLevel = 25;
        var result = _validator.Validate(data);
        Assert.Contains(result.Warnings, w => w.Code == "UNUSUAL_STARTING_LEVEL");
    }

    // --- ValidateForRestore ---

    [Fact]
    public void ValidateForRestore_MismatchedCampaignId_AddsWarning()
    {
        var data = CreateValidSnapshotData();
        var differentId = Guid.NewGuid();
        var result = _validator.ValidateForRestore(data, differentId);
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Code == "CAMPAIGN_ID_MISMATCH");
    }

    [Fact]
    public void ValidateForRestore_MatchingCampaignId_NoWarning()
    {
        var data = CreateValidSnapshotData();
        var result = _validator.ValidateForRestore(data, data.Campaign.Id);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "CAMPAIGN_ID_MISMATCH");
    }

    // --- ValidateImport ---

    [Fact]
    public void ValidateImport_ScriptInjection_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.Name = "<script>alert('xss')</script>";
        var result = _validator.ValidateImport(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "POTENTIALLY_MALICIOUS_CONTENT");
    }

    [Fact]
    public void ValidateImport_JavascriptUri_ReturnsError()
    {
        var data = CreateValidSnapshotData();
        data.Campaign.Description = "javascript:alert(1)";
        var result = _validator.ValidateImport(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "POTENTIALLY_MALICIOUS_CONTENT");
    }

    [Fact]
    public void ValidateImport_CleanData_ReturnsSuccess()
    {
        var data = CreateValidSnapshotData();
        var result = _validator.ValidateImport(data);
        Assert.True(result.IsValid);
    }

    // --- Helper ---

    private static CharacterStats ValidStats() => new()
    {
        Strength = 10, Dexterity = 10, Constitution = 10,
        Intelligence = 10, Wisdom = 10, Charisma = 10,
        HitPoints = 10, MaxHitPoints = 10
    };
}
