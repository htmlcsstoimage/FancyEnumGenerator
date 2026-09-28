using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class CollectionTests
{
    [Fact]
    public void CachedValuesAndAsSpanAgree()
    {
        var values = new List<Cached>();
        foreach (var value in Cached.Values)
        {
            values.Add(value);
        }
        Assert.Equal([Cached.A, Cached.B, Cached.C], values); // Hidden is ExcludeFromValues
        Assert.Equal(values, Cached.AsSpan.ToArray());
    }

    [Fact]
    public void AsSpanPointsAtTheSameStaticStorageEveryTime()
    {
        // A span over a by-value copy would dangle; it must be the static field itself, every call.
        var first = Cached.AsSpan;
        var second = Cached.AsSpan;
        Assert.True(Unsafe.AreSame(ref MemoryMarshal.GetReference(first), ref MemoryMarshal.GetReference(second)));
    }

    [Fact]
    public void PlainArrayValuesAreReadOnly()
    {
        IReadOnlyList<PlainArray> values = PlainArray.Values;
        Assert.Equal([PlainArray.A, PlainArray.B], values);
        Assert.Equal([PlainArray.A, PlainArray.B], PlainArray.AsSpan.ToArray());
    }

    [Fact]
    public void ExcludedMembersStillFormatAndParse()
    {
        Assert.Equal("Hidden", Cached.Hidden.ToStringFancy());
        Assert.True(Cached.TryParseFancy("Hidden", out var hidden));
        Assert.Equal(Cached.Hidden, hidden);
    }
}
