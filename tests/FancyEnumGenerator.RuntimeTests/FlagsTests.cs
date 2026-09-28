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
    public void ParsesSingleAndDeclaredCompositeNamesOnly()
    {
        Assert.True(Permission.TryParseFancy("ReadWrite", out var composite));
        Assert.Equal(Permission.ReadWrite, composite);
        Assert.False(Permission.TryParseFancy("Read|Write", out _));
    }
}
