using System;
using System.Collections.Generic;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class FlagsTests
{
    [Theory]
    [InlineData(Permission.None, "None")]
    [InlineData(Permission.Read, "Read")]
    [InlineData(Permission.Read | Permission.Execute, "Read|Execute")]
    [InlineData(Permission.Read | Permission.Write | Permission.Execute, "Read|Write|Execute")]
    [InlineData(Permission.Read | Permission.Write, "ReadWrite")] // a declared composite uses its own name
    [InlineData((Permission)8, "")] // an undeclared bit
    [InlineData(Permission.Read | (Permission)8, "")]
    public void ToStringFancy(Permission value, string expected) => Assert.Equal(expected, value.ToStringFancy());

    [Fact]
    public void CustomSeparator() => Assert.Equal("Write,Execute", (Permission.Write | Permission.Execute).ToStringFancy(','));

    [Fact]
    public void MatchesEnumToStringForDeclaredMembers()
    {
        foreach (var permission in Enum.GetValues<Permission>())
        {
            Assert.Equal(permission.ToString(), permission.ToStringFancy());
        }
    }

    [Fact]
    public void HasFlagFancyMatchesHasFlag()
    {
        for (var raw = 0; raw < 8; raw++)
        {
            var value = (Permission)raw;
            foreach (var flag in Enum.GetValues<Permission>())
            {
                Assert.Equal(value.HasFlag(flag), value.HasFlagFancy(flag));
            }
        }
    }

    [Fact]
    public void ListFlagMembers()
    {
        (Permission.Read | Permission.Execute | (Permission)8).ListFlagMembers(out var values, out var length);
        var listed = new List<Permission>();
        for (var index = 0; index < length; index++)
        {
            listed.Add(values[index]);
        }
        Assert.Equal([Permission.Read, Permission.Execute], listed);
    }

    [Fact]
    public void TryFormatHandlesEveryCombinationWithinLongestCharLength()
    {
        Span<char> buffer = stackalloc char[Permission.LongestCharLength];
        for (var raw = 0; raw < 8; raw++)
        {
            var value = (Permission)raw;
            Assert.True(value.TryFormat(buffer, out var written));
            Assert.Equal(value.ToStringFancy(), buffer[..written].ToString());
        }
    }

    [Fact]
    public void TryFormatFailsCleanlyWhenTooSmall()
    {
        Span<char> buffer = stackalloc char[3];
        Assert.False(Permission.Execute.TryFormat(buffer, out var written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryFormatCombinationFailsCleanlyWhenTooSmall()
    {
        Span<char> buffer = stackalloc char["Read|Execute".Length - 1];
        buffer.Fill('#');
        Assert.False((Permission.Read | Permission.Execute).TryFormat(buffer, out var written));
        Assert.Equal(0, written);
        Assert.Equal(new string('#', buffer.Length), buffer.ToString()); // nothing written
    }

    [Fact]
    public void TryFormatCombinationWithSeparator()
    {
        Span<char> buffer = stackalloc char[Permission.LongestCharLength];
        Assert.True((Permission.Write | Permission.Execute).TryFormat(buffer, out var written, ','));
        Assert.Equal("Write,Execute", buffer[..written].ToString());
    }

    [Fact]
    public void TryFormatUndeclaredBitsWritesNothing()
    {
        Span<char> buffer = stackalloc char[Permission.LongestCharLength];
        Assert.True((Permission.Read | (Permission)8).TryFormat(buffer, out var written));
        Assert.Equal(0, written); // the same "" ToStringFancy gives
    }

    [Theory]
    [InlineData(0, true, true)]     // the Unknown member itself
    [InlineData(1, false, false)]   // a declared flag
    [InlineData(3, false, false)]   // a declared composite (AB)
    [InlineData(5, true, false)]    // A | C: not declared, but a combination
    [InlineData(7, true, false)]    // every flag
    [InlineData(33, true, true)]    // A plus an undeclared bit
    [InlineData(8, true, true)]     // only an undeclared bit
    [InlineData(24, false, false)]  // Weird: declared, though no single-bit flag covers it
    [InlineData(25, true, true)]    // Weird | A: bits no flag covers, and not declared
    public void IsUnknownAndNotCombined(int raw, bool isUnknown, bool isUnknownAndNotCombined)
    {
        var value = (FlagsWithUnknown)raw;
        Assert.Equal(isUnknown, value.IsUnknown);
        Assert.Equal(isUnknownAndNotCombined, value.IsUnknownAndNotCombined);
    }

    [Theory]
    [InlineData(0, 0, 1)]    // Unknown; the NonUnknown variant gives FirstNonUnknown (A)
    [InlineData(5, 5, 5)]    // A | C: an undeclared combination is kept
    [InlineData(3, 3, 3)]    // AB, declared
    [InlineData(24, 24, 24)] // Weird: declared, found by the lookup rather than the mask
    [InlineData(33, 0, 1)]   // an undeclared bit
    [InlineData(25, 0, 1)]   // Weird | A: not declared, and bits no flag covers
    public void FromUnderlyingAcceptsCombinations(int raw, int expected, int expectedNonUnknown)
    {
        Assert.Equal((FlagsWithUnknown)expected, FlagsWithUnknown.FromUnderlying(raw));
        Assert.Equal((FlagsWithUnknown)expectedNonUnknown, FlagsWithUnknown.FromUnderlyingNonUnknown(raw));
    }

    [Fact]
    public void FromUnderlyingAcceptsCombinationsWithoutAnUnknownMember()
    {
        Assert.Equal(Permission.Read | Permission.Execute, Permission.FromUnderlying(5));
        Assert.Equal(Permission.None, Permission.FromUnderlying(0));
        Assert.Equal(Permission.None, Permission.FromUnderlying(8)); // undeclared bit: default
    }

    [Fact]
    public void DefinedBitsMaskAcrossUnderlyingTypes()
    {
        Assert.Equal("Low|High", (ByteFlags.Low | ByteFlags.High).ToStringFancy());
        Assert.Equal("", (ByteFlags.Low | (ByteFlags)2).ToStringFancy());
        Assert.Equal("Low|Sign", (SignFlags.Low | SignFlags.Sign).ToStringFancy());
        Assert.Equal("", (SignFlags.Sign | (SignFlags)2).ToStringFancy());
        Assert.Equal("Low|Top", (WideFlags.Low | WideFlags.Top).ToStringFancy());
        Assert.Equal("", (WideFlags.Top | (WideFlags)(1UL << 40)).ToStringFancy());

        Span<char> buffer = stackalloc char[WideFlags.LongestCharLength];
        Assert.True((WideFlags.Low | WideFlags.Top).TryFormat(buffer, out var written));
        Assert.Equal("Low|Top", buffer[..written].ToString());
        Assert.True((WideFlags.Top | (WideFlags)(1UL << 40)).TryFormat(buffer, out written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryFormatCombinationDoesNotAllocate()
    {
        var buffer = new char[Permission.LongestCharLength];
        var value = Permission.Read | Permission.Write | Permission.Execute;
        value.TryFormat(buffer, out _); // warm up (JIT)
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            value.TryFormat(buffer, out _);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void ParsesSingleAndDeclaredCompositeNamesOnly()
    {
        Assert.True(Permission.TryParseFancy("ReadWrite", out var composite));
        Assert.Equal(Permission.ReadWrite, composite);
        Assert.False(Permission.TryParseFancy("Read|Write", out _));
    }
}
