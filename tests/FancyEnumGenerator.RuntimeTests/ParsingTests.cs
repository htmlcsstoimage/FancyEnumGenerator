using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

/// <summary>
/// Station has enough same-length and long names to reach every parser strategy (direct compares, the length switch,
/// and packed-ASCII hashing with a tail compare), and is case-insensitive by default with UTF-8 parsing on.
/// </summary>
public class ParsingTests
{
    public static IEnumerable<TheoryDataRow<Station>> Members => Enum.GetValues<Station>().Select(static station => new TheoryDataRow<Station>(station));

    [Theory]
    [MemberData(nameof(Members))]
    public void RoundTripsInAnyCase(Station station)
    {
        var name = station.ToStringFancy();
        foreach (var input in new[] { name, name.ToUpperInvariant(), name.ToLowerInvariant() })
        {
            Assert.True(Station.TryParseFancy(input, out var fromChars), input);
            Assert.Equal(station, fromChars);
            Assert.True(Station.TryParseFancy(Encoding.UTF8.GetBytes(input), out var fromBytes), input);
            Assert.Equal(station, fromBytes);
        }
    }

    [Theory]
    [MemberData(nameof(Members))]
    public void CaseSensitiveOverloadRequiresExactCase(Station station)
    {
        var lower = station.ToStringFancy().ToLowerInvariant();
        Assert.False(Station.TryParseFancy(lower, ignoreCase: false, out _));
        Assert.False(Station.TryParseFancy(Encoding.UTF8.GetBytes(lower), ignoreCase: false, out _));
        Assert.True(Station.TryParseFancy(station.ToStringFancy(), ignoreCase: false, out var exact));
        Assert.Equal(station, exact);
    }

    /// <summary>Every single-character edit of every name: hash collisions or an off-by-one in a tail compare would let one through.</summary>
    [Theory]
    [MemberData(nameof(Members))]
    public void RejectsNearMisses(Station station)
    {
        var name = station.ToStringFancy();
        var misses = new List<string> { name[..^1], name + "x", "x" + name };
        for (var index = 0; index < name.Length; index++)
        {
            var chars = name.ToCharArray();
            chars[index] = chars[index] == 'Z' ? 'Y' : 'Z';
            misses.Add(new string(chars));
            chars[index] = 'é'; // non-ASCII, and two bytes in UTF-8
            misses.Add(new string(chars));
        }
        foreach (var miss in misses.Where(miss => !string.Equals(miss, name, StringComparison.OrdinalIgnoreCase)))
        {
            Assert.False(Station.TryParseFancy(miss, out _), miss);
            Assert.False(Station.TryParseFancy(Encoding.UTF8.GetBytes(miss), out _), miss);
        }
    }

    [Fact]
    public void NonAsciiNamesIgnoringCaseMatchOrdinalIgnoreCase()
    {
        Assert.True(Accents.TryParseFancy("CAFÉ", ignoreCase: true, out var cafe));
        Assert.Equal(Accents.Café, cafe);
        Assert.True(Accents.TryParseFancy("naïve", ignoreCase: true, out var naive));
        Assert.Equal(Accents.Naïve, naive);
        Assert.True(Accents.TryParseFancy("CAFE", ignoreCase: true, out var plain));
        Assert.Equal(Accents.Cafe, plain); // "é" doesn't fold to "e"
        Assert.False(Accents.TryParseFancy("CAFÉ", out _)); // case-sensitive by default
    }

    [Fact]
    public void NamesDifferingOnlyByCase()
    {
        Assert.True(Accents.TryParseFancy("ALPHA", out var upper));
        Assert.Equal(Accents.ALPHA, upper);
        Assert.True(Accents.TryParseFancy("Alpha", out var mixed));
        Assert.Equal(Accents.Alpha, mixed);
        Assert.True(Accents.TryParseFancy("alpha", ignoreCase: true, out var either));
        Assert.True(either is Accents.Alpha or Accents.ALPHA);
        Assert.True(Accents.TryParseFancy("alpha"u8, ignoreCase: true, out var eitherBytes));
        Assert.True(eitherBytes is Accents.Alpha or Accents.ALPHA);
    }

    [Theory]
    [InlineData("TE", true)]
    [InlineData("Te", true)]
    [InlineData("te", false)]
    [InlineData("tE", false)]
    public void CaseSensitiveOverloadMatchesEverySpellingExactly(string input, bool expected)
    {
        Assert.Equal(expected, FewWireNames.TryParseFancy(input, ignoreCase: false, out _));
        Assert.Equal(expected, ManyWireNames.TryParseFancy(input, ignoreCase: false, out _));
        Assert.True(FewWireNames.TryParseFancy(input, out var few)); // the default for these enums ignores case
        Assert.Equal(FewWireNames.Te, few);
        Assert.True(ManyWireNames.TryParseFancy(input, out var many));
        Assert.Equal(ManyWireNames.Te, many);
    }

    [Fact]
    public void NonAsciiNamesFromUtf8()
    {
        Assert.True(Accents.TryParseFancy("Résumé"u8, out var resume));
        Assert.Equal(Accents.Résumé, resume);
        Assert.False(Accents.TryParseFancy("Resume"u8, out _));
    }

    [Fact]
    public void ParseOrUnknownFromBytes()
    {
        Assert.Equal(Station.Hub, Station.ParseOrUnknown("hub"u8));
        Assert.Equal(Station.Unknown, Station.ParseOrUnknown("nowhere"u8));
    }

    [Theory]
    [InlineData("West", "Coast", true)]
    [InlineData("west", "COAST", true)] // case-insensitive by default
    [InlineData("", "Hub", true)]
    [InlineData("WestCoastRegion0", "5", true)]
    [InlineData("WestCoastRegion0", "6", false)]
    [InlineData("East", "Bay", false)]
    [InlineData("HubHub", "", false)]
    public void IsValidPrefixFancy(string prefix, string next, bool expected) =>
        Assert.Equal(expected, Station.IsValidPrefixFancy(Encoding.UTF8.GetBytes(prefix), Encoding.UTF8.GetBytes(next)));

    [Fact]
    public void TryFormatAndLongestCharLength()
    {
        Assert.Equal("WestCoastRegion01".Length, Station.LongestCharLength);
        Span<char> buffer = stackalloc char[Station.LongestCharLength];
        foreach (var station in Enum.GetValues<Station>())
        {
            Assert.True(station.TryFormat(buffer, out var written));
            Assert.Equal(station.ToStringFancy(), buffer[..written].ToString());
        }
    }
}
