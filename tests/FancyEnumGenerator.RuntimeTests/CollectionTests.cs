using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class CollectionTests
{
    [Fact]
    public void SpanValuesLeaveOutUnknownAndExcludedMembers()
    {
        Assert.Equal([Spanned.A, Spanned.B, Spanned.C], Spanned.Values.ToArray()); // Hidden is ExcludeFromValues
    }

    [Fact]
    public void SpanValuesAreStaticDataInTheAssembly()
    {
        // The same memory every call, and nothing allocated: the compiler stores the members as static data.
        Assert.True(Unsafe.AreSame(ref MemoryMarshal.GetReference(Spanned.Values), ref MemoryMarshal.GetReference(Spanned.Values)));
        _ = Spanned.Values.Length;
        // Unoptimized (Debug) code calls RuntimeHelpers.CreateSpan the slow way, which allocates; optimized code is free.
        var optimized = typeof(CollectionTests).Assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled != true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        long sum = 0;
        for (var i = 0; i < 1_000; i++)
        {
            foreach (var value in Spanned.Values)
            {
                sum += (long)value;
            }
        }
        if (optimized)
        {
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(1_000 * (1 + 2 + 4), sum);
    }

    [Fact]
    public void InlineArrayValuesAreAnIndependentCopy()
    {
        var values = Inline.Values;
        values[0] = Inline.Hidden;
        Assert.Equal(Inline.A, Inline.Values[0]); // changing a copy never reaches the next caller
        Assert.Equal([Inline.A, Inline.B, Inline.C], Inline.AsSpan.ToArray());
    }

    [Fact]
    public async Task InlineArrayValuesSurviveAnAwait()
    {
        var values = Inline.Values;
        await Task.Yield();
        var seen = new List<Inline>();
        foreach (var value in values)
        {
            seen.Add(value);
        }
        Assert.Equal([Inline.A, Inline.B, Inline.C], seen);
    }

    [Fact]
    public void StaticCollectionValuesAndAsSpanAgree()
    {
        IReadOnlyList<Cached> values = Cached.Values;
        Assert.Equal([Cached.A, Cached.B, Cached.C], values);
        Assert.Equal(values, Cached.AsSpan.ToArray());
        Assert.Same(Cached.Values, Cached.Values); // cached once, not rebuilt per access
    }

    [Fact]
    public void StaticCollectionAsSpanPointsAtTheCachedArray()
    {
        Assert.True(Unsafe.AreSame(ref MemoryMarshal.GetReference(Cached.AsSpan), ref MemoryMarshal.GetReference(Cached.AsSpan)));
    }

    [Fact]
    public void StaticCollectionWithoutInlineArrays()
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
