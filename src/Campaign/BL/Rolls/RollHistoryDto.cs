namespace DnDiscord.Campaign.BL.Rolls;

public class RollHistoryDto
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string DiceType { get; set; } = "d20";
    public int Value { get; set; }
    public string? Label { get; set; }
    public DateTime RolledAt { get; set; }
}
