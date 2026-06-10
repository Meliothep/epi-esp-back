namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// A saved map. Can be tied to a campaign (<see cref="CampaignId"/> set) or
/// standalone (<see cref="CampaignId"/> null). Always owned by a user (<see cref="OwnerId"/>).
/// Persists the <see cref="Data"/> blob (the front's <c>SavedMapData</c> JSON —
/// tiles, props, lights, …).
/// </summary>
public class CampaignMap
{
    public Guid Id { get; set; }

    /// <summary>
    /// Campaign this map belongs to. Null for standalone (user-owned) maps.
    /// When set, deleting the campaign restricts deletion of the map.
    /// </summary>
    public Guid? CampaignId { get; set; }

    /// <summary>User who created this map. Always set.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Whether this map is visible in the community browser.
    /// False by default — no UI surface yet, reserved for future feature.
    /// </summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>Short human-readable label shown in the DM's map picker.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Serialised <c>SavedMapData</c> blob (jsonb in Postgres).</summary>
    public string Data { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
