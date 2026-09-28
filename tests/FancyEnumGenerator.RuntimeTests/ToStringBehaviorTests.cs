using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class ToStringBehaviorTests
{
    [Fact]
    public void DescriptionAttribute()
    {
        Assert.Equal("First one", ByDescription.First.ToStringFancy());
        Assert.Equal("Second", ByDescription.Second.ToStringFancy());
        Assert.True(ByDescription.TryParseFancy("First one", out var byText));
        Assert.True(ByDescription.TryParseFancy("First", out var byName));
        Assert.Equal(ByDescription.First, byText);
        Assert.Equal(ByDescription.First, byName);
    }

    [Fact]
    public void DisplayAttribute()
    {
        Assert.Equal("First one", ByDisplay.First.ToStringFancy());
        Assert.Equal("Second", ByDisplay.Second.ToStringFancy());
    }

    [Fact]
    public void EnumMemberAttribute()
    {
        Assert.Equal("first_one", ByEnumMember.First.ToStringFancy());
        Assert.Equal("Second", ByEnumMember.Second.ToStringFancy());
    }

    [Fact]
    public void JsonStringEnumMemberNameAttribute()
    {
        Assert.Equal("first-one", ByJsonName.First.ToStringFancy());
        Assert.Equal("Second", ByJsonName.Second.ToStringFancy());
    }

    [Fact]
    public void NameCasing()
    {
        Assert.Equal("firstone", ByLower.FirstOne.ToStringFancy());
        Assert.Equal("FIRSTONE", ByUpper.FirstOne.ToStringFancy());
        Assert.Equal("secondone".Length, ByLower.LongestCharLength);
    }

    [Fact]
    public void CustomFieldRequired()
    {
        Assert.Equal("F1", ByRequiredField.First.ToStringFancy());
        Assert.True(ByRequiredField.TryParseFancy("S2", out var second));
        Assert.Equal(ByRequiredField.Second, second);
    }
}
