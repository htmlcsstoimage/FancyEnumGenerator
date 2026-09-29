using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class ShapeTests
{
    [Fact]
    public void NonContiguousSignedEnumWithoutUnknown()
    {
        Assert.Equal(Sparse.Seven, Sparse.FromUnderlying(7));
        Assert.Equal(Sparse.Negative, Sparse.FromUnderlying(-5));
        Assert.Equal(default, Sparse.FromUnderlying(8));
        Assert.Equal(Sparse.Negative, Sparse.FirstNonUnknown); // the lowest-valued member
        Assert.Equal("Hundred", Sparse.Hundred.ToStringFancy());
        Assert.Equal((short)-5, Sparse.Negative.AsUnderlying);
    }

    [Fact]
    public void ParseOrDefaultFallsBackToFirstMember() => Assert.Equal(Sparse.Negative, Sparse.ParseOrDefault("nope"));

#pragma warning disable CS0612, CS0618 // deliberately touching obsolete members
    [Fact]
    public void ObsoleteMembersAreExcludedByDefault()
    {
        Assert.Equal("", WithObsolete.Legacy.ToStringFancy());
        Assert.False(WithObsolete.TryParseFancy("Legacy", out _));
        Assert.True(WithObsolete.Legacy.IsUnknown);
        Assert.Equal(2, WithObsolete.Length);
    }

    [Fact]
    public void IncludeObsoleteKeepsThem()
    {
        Assert.Equal("Older", KeepsObsolete.Older.ToStringFancy());
        Assert.True(KeepsObsolete.TryParseFancy("Older", out var older));
        Assert.Equal(KeepsObsolete.Older, older);
    }

    [Fact]
    public void ObsoleteMemberInTheMiddleLeavesAGapThatStillWorks()
    {
        Assert.Equal(ObsoleteInTheMiddle.Last, ObsoleteInTheMiddle.FromUnderlying(3));
        Assert.Equal(ObsoleteInTheMiddle.Unknown, ObsoleteInTheMiddle.FromUnderlying(2)); // the excluded member's value
        Assert.True(ObsoleteInTheMiddle.Middle.IsUnknown);
        Assert.False(ObsoleteInTheMiddle.Last.IsUnknown);
        Assert.Equal("Last", ObsoleteInTheMiddle.Last.ToStringFancy());
    }
#pragma warning restore CS0612, CS0618

    [Fact]
    public void NestedEnum()
    {
        Assert.Equal("Only", Outer.Nested.Only.ToStringFancy());
        Assert.Equal(2, Outer_NestedFancyEnumExtensions.Length);
        Assert.Equal((byte)1, Outer.Nested.Only.AsUnderlying);
    }
}
