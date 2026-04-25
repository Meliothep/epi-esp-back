namespace Multiplayer.Models.Messages;

public class GoldGrantedPublicPayload
{
    public Guid TargetUserId { get; set; }
    public string TargetCharacterName { get; set; } = string.Empty;
    public string CurrencyType { get; set; } = "gp";
    public int Amount { get; set; }
}
