using System.ComponentModel.DataAnnotations;

namespace DnDiscord.Campaign.BL.Maps;

public record CampaignMapDto(
    Guid Id,
    Guid CampaignId,
    string Name,
    string Data,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public class CreateCampaignMapRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Data { get; set; } = string.Empty;
}

public class UpdateCampaignMapRequest
{
    [StringLength(200, MinimumLength = 1)]
    public string? Name { get; set; }

    public string? Data { get; set; }
}
