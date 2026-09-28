using System;
using System.Collections.Generic;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class BasicTests
{
    [Fact]
    public void ToStringFancyMatchesEnumToString()
    {
        foreach (var fruit in Enum.GetValues<Fruit>())
        {
            Assert.Equal(fruit.ToString(), fruit.ToStringFancy());
        }
    }

    [Fact]
    public void TryParseFancyRoundTripsAndMatchesEnumTryParseForNames()
    {
        foreach (var fruit in Enum.GetValues<Fruit>())
        {
            Assert.True(Fruit.TryParseFancy(fruit.ToStringFancy(), out var parsed));
            Assert.Equal(fruit, parsed);
            Assert.True(Enum.TryParse<Fruit>(fruit.ToString(), out var bcl));
            Assert.Equal(bcl, parsed);
        }
    }

    [Fact]
    public void ParsingIsCaseSensitiveByDefault()
    {
        Assert.False(Fruit.TryParseFancy("apple", out _));
        Assert.True(Fruit.TryParseFancy("apple", ignoreCase: true, out var parsed));
        Assert.Equal(Fruit.Apple, parsed);
    }

    [Theory]
    [InlineData("1")] // Enum.TryParse accepts numeric strings; TryParseFancy deliberately doesn't.
    [InlineData(" Apple")]
    [InlineData("Apple ")]
    [InlineData("Appl")]
    [InlineData("Apples")]
    [InlineData("")]
    public void TryParseFancyRejectsAnythingButAnExactMatch(string input)
    {
        Assert.False(Fruit.TryParseFancy(input, out var result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void ParseOrUnknown()
    {
        Assert.Equal(Fruit.Banana, Fruit.ParseOrUnknown("Banana"));
        Assert.Equal(Fruit.Unknown, Fruit.ParseOrUnknown("Durian"));
        Assert.Equal(Fruit.Banana, Fruit.ParseOrUnknown("BANANA", ignoreCase: true));
    }

    [Theory]
    [InlineData(0, Fruit.Unknown)]
    [InlineData(2, Fruit.Banana)]
    [InlineData(3, Fruit.Cherry)]
    [InlineData(4, Fruit.Unknown)]
    [InlineData(-1, Fruit.Unknown)]
    [InlineData(int.MaxValue, Fruit.Unknown)]
    public void FromUnderlying(int underlying, Fruit expected) => Assert.Equal(expected, Fruit.FromUnderlying(underlying));

    [Theory]
    [InlineData(0, Fruit.Apple)]
    [InlineData(2, Fruit.Banana)]
    [InlineData(99, Fruit.Apple)]
    public void FromUnderlyingNonUnknown(int underlying, Fruit expected) => Assert.Equal(expected, Fruit.FromUnderlyingNonUnknown(underlying));

    [Fact]
    public void UnknownHandling()
    {
        Assert.True(Fruit.Unknown.IsUnknown);
        Assert.True(((Fruit)99).IsUnknown);
        Assert.False(Fruit.Cherry.IsUnknown);
        Assert.Equal(Fruit.Apple, Fruit.Unknown.ValueOrDefaultIfUnknown);
        Assert.Equal(Fruit.Cherry, Fruit.Cherry.ValueOrDefaultIfUnknown);
        Assert.Equal(1, Fruit.Unknown.AsUnderlyingNonUnknown);
        Assert.Equal(3, Fruit.Cherry.AsUnderlying);
    }

    [Fact]
    public void UndeclaredValueFormatsAsEmpty() => Assert.Equal("", ((Fruit)99).ToStringFancy());

    [Fact]
    public void Metadata()
    {
        // The constants class is usable where a compile-time constant is required.
        const int length = FruitExtensions.Length;
        const Fruit first = FruitExtensions.FirstNonUnknown; // a const, so the default output has no static state
        Assert.Equal(4, length);
        Assert.Equal(Fruit.Apple, first);
        Assert.Equal(4, Fruit.Length);
        Assert.Equal("Unknown".Length, Fruit.LongestCharLength); // Unknown formats too
        Assert.Equal(Fruit.Apple, Fruit.FirstNonUnknown);
        Assert.Equal(1, Fruit.FirstNonUnknownOrdinal);
    }

    [Fact]
    public void ValuesExcludesUnknown()
    {
        var values = new List<Fruit>();
        foreach (var fruit in Fruit.Values)
        {
            values.Add(fruit);
        }
        Assert.Equal([Fruit.Apple, Fruit.Banana, Fruit.Cherry], values);
    }
}
