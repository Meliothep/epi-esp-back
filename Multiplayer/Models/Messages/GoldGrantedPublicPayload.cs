namespace Multiplayer.Models.Messages;

public record GoldGrantedPublicPayload
{
    public Guid TargetUserId { get; init; }
    public string TargetCharacterName { get; init; } = string.Empty;
    public CurrencyType CurrencyType { get; init; } = CurrencyType.Gp;
    public int Amount { get; init; }
}
