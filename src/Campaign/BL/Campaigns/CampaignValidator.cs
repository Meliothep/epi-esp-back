using DnDiscord.Campaign.BL.Campaigns.DTOs;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.Extensions.Logging;
using CampaignEntity = DnDiscord.Campaign.DataAccess.Models.Campaign;

namespace DnDiscord.Campaign.BL.Campaigns;

/// <summary>
/// Validates campaign data and permissions.
/// </summary>
public interface ICampaignValidator
{
    /// <summary>
    /// Validates a create campaign request.
    /// </summary>
    ValidationResult ValidateCreate(CreateCampaignRequest request);
    
    /// <summary>
    /// Validates an update campaign request.
    /// </summary>
    ValidationResult ValidateUpdate(UpdateCampaignRequest request);
    ValidationResult ValidateCampaignTreeDefinition(EditCampaignManager request);

    /// <summary>
    /// Checks if the user can modify the campaign.
    /// </summary>
    bool CanModify(CampaignEntity campaign, Guid userId);
    
    /// <summary>
    /// Checks if the user can delete the campaign.
    /// </summary>
    bool CanDelete(CampaignEntity campaign, Guid userId);
    
    /// <summary>
    /// Checks if the user can view the campaign.
    /// </summary>
    bool CanView(CampaignEntity campaign, Guid userId, bool isMember);
    
    /// <summary>
    /// Checks if the user can manage members.
    /// </summary>
    bool CanManageMembers(CampaignEntity campaign, Guid userId);
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationError> Errors { get; init; } = [];
    
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
/// Implementation of campaign validator.
/// </summary>
public class CampaignValidator : ICampaignValidator
{
    private readonly ILogger<CampaignValidator> _logger;
    
    // Validation limits
    private const int MinNameLength = 3;
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 4000;
    private const int MinPlayers = 1;
    private const int MaxPlayers = 20;
    
    public CampaignValidator(ILogger<CampaignValidator> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateCreate(CreateCampaignRequest request)
    {
        var errors = new List<ValidationError>();
        
        // Name validation
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new ValidationError("REQUIRED", "Campaign name is required", "Name"));
        }
        else if (request.Name.Length < MinNameLength)
        {
            errors.Add(new ValidationError("TOO_SHORT", $"Campaign name must be at least {MinNameLength} characters", "Name"));
        }
        else if (request.Name.Length > MaxNameLength)
        {
            errors.Add(new ValidationError("TOO_LONG", $"Campaign name cannot exceed {MaxNameLength} characters", "Name"));
        }
        
        // Description validation
        if (request.Description?.Length > MaxDescriptionLength)
        {
            errors.Add(new ValidationError("TOO_LONG", $"Description cannot exceed {MaxDescriptionLength} characters", "Description"));
        }
        
        // MaxPlayers validation
        if (request.MaxPlayers < MinPlayers || request.MaxPlayers > MaxPlayers)
        {
            errors.Add(new ValidationError("OUT_OF_RANGE", $"MaxPlayers must be between {MinPlayers} and {MaxPlayers}", "MaxPlayers"));
        }
        
        // ImageUrl validation
        if (!string.IsNullOrEmpty(request.ImageUrl) && !IsValidUrl(request.ImageUrl))
        {
            errors.Add(new ValidationError("INVALID_URL", "ImageUrl must be a valid URL", "ImageUrl"));
        }
        
        // Status validation
        if (request.Status == CampaignStatus.Archived)
        {
            errors.Add(new ValidationError("INVALID_STATUS", "Cannot create a campaign with Archived status", "Status"));
        }
        
        if (errors.Count > 0)
        {
            _logger.LogWarning("Campaign create validation failed with {ErrorCount} errors", errors.Count);
        }
        
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
    }
    
    /// <inheritdoc />
    public ValidationResult ValidateUpdate(UpdateCampaignRequest request)
    {
        var errors = new List<ValidationError>();
        
        // Name validation (if provided)
        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ValidationError("REQUIRED", "Campaign name cannot be empty", "Name"));
            }
            else if (request.Name.Length < MinNameLength)
            {
                errors.Add(new ValidationError("TOO_SHORT", $"Campaign name must be at least {MinNameLength} characters", "Name"));
            }
            else if (request.Name.Length > MaxNameLength)
            {
                errors.Add(new ValidationError("TOO_LONG", $"Campaign name cannot exceed {MaxNameLength} characters", "Name"));
            }
        }
        
        // Description validation (if provided)
        if (request.Description?.Length > MaxDescriptionLength)
        {
            errors.Add(new ValidationError("TOO_LONG", $"Description cannot exceed {MaxDescriptionLength} characters", "Description"));
        }
        
        // MaxPlayers validation (if provided)
        if (request.MaxPlayers.HasValue && (request.MaxPlayers < MinPlayers || request.MaxPlayers > MaxPlayers))
        {
            errors.Add(new ValidationError("OUT_OF_RANGE", $"MaxPlayers must be between {MinPlayers} and {MaxPlayers}", "MaxPlayers"));
        }
        
        // ImageUrl validation (if provided)
        if (!string.IsNullOrEmpty(request.ImageUrl) && !IsValidUrl(request.ImageUrl))
        {
            errors.Add(new ValidationError("INVALID_URL", "ImageUrl must be a valid URL", "ImageUrl"));
        }
        
        if (errors.Count > 0)
        {
            _logger.LogWarning("Campaign update validation failed with {ErrorCount} errors", errors.Count);
        }
        
        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
    }

    public ValidationResult ValidateCampaignTreeDefinition(EditCampaignManager request)
    {
        var errors = new List<ValidationError>();

        // Name validation (if provided)
        if (request.CampaignTreeDefinition != null)
        {
            if (string.IsNullOrWhiteSpace(request.CampaignTreeDefinition))
            {
                errors.Add(new ValidationError("REQUIRED", "Campaign TreeDefinition cannot be empty", "Name"));
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Campaign update validation failed with {ErrorCount} errors", errors.Count);
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors.ToArray());
    }

    /// <inheritdoc />
    public bool CanModify(CampaignEntity campaign, Guid userId)
    {
        // Only the DM can modify the campaign
        return campaign.DungeonMasterId == userId;
    }
    
    /// <inheritdoc />
    public bool CanDelete(CampaignEntity campaign, Guid userId)
    {
        // Only the DM can delete the campaign
        return campaign.DungeonMasterId == userId;
    }
    
    /// <inheritdoc />
    public bool CanView(CampaignEntity campaign, Guid userId, bool isMember)
    {
        // Can view if:
        // - Campaign is public
        // - User is the DM
        // - User is a member
        return campaign.IsPublic || campaign.DungeonMasterId == userId || isMember;
    }
    
    /// <inheritdoc />
    public bool CanManageMembers(CampaignEntity campaign, Guid userId)
    {
        // Only the DM can manage members
        return campaign.DungeonMasterId == userId;
    }
    
    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}

