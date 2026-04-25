using Multiplayer.Services;
using Xunit;

namespace DnDiscordAPI.Tests.Unit.Multiplayer;

/// <summary>
/// Unit tests for CurrencyTypeHelper — covers the normalisation + whitelist validation
/// that DmGrantGold in GameHub delegates to. These tests replace the removed
/// AdjustCurrency_NormalizesInput adapter test and ensure the hub-side rule is pinned.
/// </summary>
public class CurrencyTypeHelperTests
{
    // ── Normalize ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("gp", "gp")]
    [InlineData("GP", "gp")]       // uppercase
    [InlineData("  gp  ", "gp")]   // surrounding whitespace
    [InlineData("CP", "cp")]
    [InlineData("PP", "pp")]
    [InlineData(null, "gp")]       // null defaults to gp
    public void Normalize_ReturnsLowercaseTrimmed(string? raw, string expected)
    {
        Assert.Equal(expected, CurrencyTypeHelper.Normalize(raw));
    }

    // ── IsValid ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cp")]
    [InlineData("sp")]
    [InlineData("ep")]
    [InlineData("gp")]
    [InlineData("pp")]
    public void IsValid_KnownTypes_ReturnsTrue(string normalized)
    {
        Assert.True(CurrencyTypeHelper.IsValid(normalized));
    }

    [Theory]
    [InlineData("xp")]
    [InlineData("GP")]    // un-normalised uppercase must NOT pass
    [InlineData("gold")]
    [InlineData("")]
    public void IsValid_UnknownOrUnnormalised_ReturnsFalse(string normalized)
    {
        Assert.False(CurrencyTypeHelper.IsValid(normalized));
    }

    // ── Normalize + IsValid together (hub flow) ──────────────────────────────

    [Theory]
    [InlineData("GP")]
    [InlineData("  gp  ")]
    [InlineData("CP")]
    [InlineData("PP")]
    public void NormalizeThenIsValid_AcceptsVariantCasing(string raw)
    {
        Assert.True(CurrencyTypeHelper.IsValid(CurrencyTypeHelper.Normalize(raw)));
    }

    [Theory]
    [InlineData("xp")]
    [InlineData("gold")]
    [InlineData("usd")]
    public void NormalizeThenIsValid_RejectsUnknown(string raw)
    {
        Assert.False(CurrencyTypeHelper.IsValid(CurrencyTypeHelper.Normalize(raw)));
    }
}
