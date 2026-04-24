namespace DnDiscord.Campaign.DataAccess.Models;

/// <summary>
/// A saved map tied to a campaign. Persists the <see cref="Data"/> blob (the front's
/// <c>SavedMapData</c> JSON — tiles, props, lights, …) so the DM can switch between
/// multiple maps mid-session instead of baking a single layout at game start.
/// </summary>
public class CampaignMap
{
    public Guid Id { get; set; }

    /// <summary>Campaign this map belongs to. Cascade-deleted with the campaign.</summary>
    public Guid CampaignId { get; set; }

    /// <summary>Short human-readable label shown in the DM's map picker.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Serialised <c>SavedMapData</c> blob (jsonb in Postgres).</summary>
    public string Data { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
