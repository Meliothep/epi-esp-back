using System.Text.Json;
using Multiplayer.Models.Messages;
using Xunit;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

public class CurrencyTypeSerializationTests
{
    [Theory]
    [InlineData(CurrencyType.Cp, "\"cp\"")]
    [InlineData(CurrencyType.Sp, "\"sp\"")]
    [InlineData(CurrencyType.Ep, "\"ep\"")]
    [InlineData(CurrencyType.Gp, "\"gp\"")]
    [InlineData(CurrencyType.Pp, "\"pp\"")]
    public void CurrencyType_SerializesAsLowercase_OnTheWire(CurrencyType currencyType, string expectedJson)
    {
        Assert.Equal(expectedJson, JsonSerializer.Serialize(currencyType));
    }

    [Theory]
    [InlineData(CurrencyType.Cp, "cp")]
    [InlineData(CurrencyType.Sp, "sp")]
    [InlineData(CurrencyType.Ep, "ep")]
    [InlineData(CurrencyType.Gp, "gp")]
    [InlineData(CurrencyType.Pp, "pp")]
    public void DmGrantGoldPayload_SerializesCurrencyTypeAsLowercase(CurrencyType currencyType, string expected)
    {
        var json = JsonSerializer.Serialize(new DmGrantGoldPayload { CurrencyType = currencyType });

        Assert.Equal(expected, GetCurrencyTypeValue(json));
    }

    [Theory]
    [InlineData("\"cp\"", CurrencyType.Cp)]
    [InlineData("\"sp\"", CurrencyType.Sp)]
    [InlineData("\"ep\"", CurrencyType.Ep)]
    [InlineData("\"gp\"", CurrencyType.Gp)]
    [InlineData("\"pp\"", CurrencyType.Pp)]
    public void CurrencyType_DeserializesLowercaseWireValues(string json, CurrencyType expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<CurrencyType>(json));
    }

    [Theory]
    [InlineData(CurrencyType.Cp, "cp")]
    [InlineData(CurrencyType.Sp, "sp")]
    [InlineData(CurrencyType.Ep, "ep")]
    [InlineData(CurrencyType.Gp, "gp")]
    [InlineData(CurrencyType.Pp, "pp")]
    public void ToWire_ReturnsLowercaseAdapterToken(CurrencyType currencyType, string expected)
    {
        Assert.Equal(expected, currencyType.ToWire());
    }

    [Fact]
    public void CurrencyType_RejectsUnknownWireValue()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CurrencyType>("\"xp\""));
    }

    [Fact]
    public void GoldGrantedPublicPayload_SerializesCurrencyTypeAsLowercase()
    {
        var json = JsonSerializer.Serialize(new GoldGrantedPublicPayload { CurrencyType = CurrencyType.Gp });

        Assert.Equal("gp", GetCurrencyTypeValue(json));
    }

    private static string GetCurrencyTypeValue(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(nameof(DmGrantGoldPayload.CurrencyType)).GetString()!;
    }
}
