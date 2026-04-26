using System.Text.Json;
using System.Text.Json.Serialization;

namespace Multiplayer.Models.Messages;

internal sealed class CurrencyTypeJsonConverter : JsonStringEnumConverter<CurrencyType>
{
    public CurrencyTypeJsonConverter() : base(JsonNamingPolicy.CamelCase) { }
}

[JsonConverter(typeof(CurrencyTypeJsonConverter))]
public enum CurrencyType
{
    Cp,
    Sp,
    Ep,
    Gp,
    Pp,
}

public static class CurrencyTypeExtensions
{
    public static string ToWire(this CurrencyType currencyType) => currencyType.ToString().ToLowerInvariant();
}
