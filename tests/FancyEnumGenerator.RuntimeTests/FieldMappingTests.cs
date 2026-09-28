using System;
using System.Text;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class FieldMappingTests
{
    [Theory]
    [InlineData(Beverage.Coffee, "Hot Coffee")]
    [InlineData(Beverage.Soda, "Sparkling")]
    [InlineData(Beverage.Juice, "?")] // unmapped -> NotMatched
    [InlineData(Beverage.Unknown, "?")]
    [InlineData((Beverage)99, "?")]
    public void StringFieldWithNotMatched(Beverage beverage, string expected) => Assert.Equal(expected, beverage.Label);

    [Theory]
    [InlineData(Beverage.Coffee, "Hot Coffee")]
    [InlineData(Beverage.Juice, "Juice")]
    public void CustomFieldFallbackToString(Beverage beverage, string expected) => Assert.Equal(expected, beverage.ToStringFancy());

    [Fact]
    public void TryParseFancyAcceptsNameAndCustomString()
    {
        Assert.True(Beverage.TryParseFancy("Hot Coffee", out var byLabel));
        Assert.True(Beverage.TryParseFancy("Coffee", out var byName));
        Assert.Equal(Beverage.Coffee, byLabel);
        Assert.Equal(Beverage.Coffee, byName);
    }

    [Theory]
    [InlineData(Beverage.Soda, "fizzy")]
    [InlineData(Beverage.Coffee, "coffee")] // NotDefined = NameOfLower
    [InlineData(Beverage.Unknown, "unknown")]
    public void NotDefinedNameFallback(Beverage beverage, string expected) => Assert.Equal(expected, beverage.Slug);

    [Fact]
    public void ReturnNullOnNotMatched()
    {
        Assert.Equal("coffee-cup", Beverage.Coffee.Emoji);
        Assert.Null(Beverage.Tea.Emoji);
    }

    [Fact]
    public void TypedFieldWithThrowOnNotMatched()
    {
        Assert.Equal(5, Beverage.Coffee.Calories);
        Assert.Equal(150, Beverage.Juice.Calories);
        Assert.Throws<ArgumentOutOfRangeException>(() => Beverage.Water.Calories);
    }

    [Fact]
    public void StaticMemberSources()
    {
        Assert.Equal("computed", Beverage.Juice.Source);
        Assert.Equal("imported", Beverage.Soda.Source);
        Assert.Equal("", Beverage.Coffee.Source);
    }

    [Fact]
    public void Utf8ValueMatchesString()
    {
        foreach (var beverage in Enum.GetValues<Beverage>())
        {
            Assert.Equal(Encoding.UTF8.GetBytes(beverage.Label), beverage.LabelBytes.ToArray());
        }
    }

    [Fact]
    public void TryFormatFieldWithinLongestCharLength()
    {
        Span<char> buffer = stackalloc char[Beverage.Label_LongestCharLength];
        foreach (var beverage in Enum.GetValues<Beverage>())
        {
            Assert.True(beverage.TryFormat_Label(buffer, out var written));
            Assert.Equal(beverage.Label, buffer[..written].ToString());
        }
        Assert.False(Beverage.Coffee.TryFormat_Label(buffer[..3], out var none));
        Assert.Equal(0, none);
    }

    [Fact]
    public void ParseFromField()
    {
        Assert.True(Beverage.TryParseFrom_Label("Green Tea", out var tea));
        Assert.Equal(Beverage.Tea, tea);
        Assert.False(Beverage.TryParseFrom_Label("green tea", out _)); // field default: case-sensitive
        Assert.True(Beverage.TryParseFrom_Label("green tea", ignoreCase: true, out _));
        Assert.False(Beverage.TryParseFrom_Label("?", out _)); // NotMatched is not a parse target
    }

    [Fact]
    public void ParseFromFieldIncludesNotDefinedFallbacks()
    {
        Assert.True(Beverage.TryParseFrom_Slug("COFFEE", out var coffee)); // this field is case-insensitive
        Assert.Equal(Beverage.Coffee, coffee);
        Assert.True(Beverage.TryParseFrom_Slug("fizzy", out var soda));
        Assert.Equal(Beverage.Soda, soda);
    }
}
