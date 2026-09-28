using System;
using FancyEnumGenerator.Attributes;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

[FancyEnum(CreateIsValidPrefix = true, CreateByteParsing = true)]
public enum FormField { Unknown, Email, DisplayName, Password, RememberMe }

/// <summary>The scanner from docs/is-valid-prefix.md, verbatim, so the documented example is known to compile and work.</summary>
public sealed class FormFieldNameScanner
{
    // Every valid prefix is at most as long as the longest name, so this never overflows.
    // (LongestCharLength counts chars; for ASCII names that equals the byte count.)
    private readonly byte[] _buffer = new byte[FormField.LongestCharLength];
    private int _length;

    /// <summary>
    /// Adds the next chunk of the name: the bytes up to '=' or the end of the read.
    /// Returns false once no known field starts this way; skip the rest of the pair (up to the next '&amp;').
    /// </summary>
    public bool Append(ReadOnlySpan<byte> chunk)
    {
        if (!FormField.IsValidPrefixFancy(_buffer.AsSpan(0, _length), chunk))
        {
            return false;
        }
        chunk.CopyTo(_buffer.AsSpan(_length));
        _length += chunk.Length;
        return true;
    }

    /// <summary>Call on reaching '=': the field this name was, or Unknown if it was only a prefix of one.</summary>
    public FormField Complete()
    {
        var field = FormField.ParseOrUnknown(_buffer.AsSpan(0, _length));
        _length = 0;
        return field;
    }
}

public class IsValidPrefixTests
{
    [Fact]
    public void NameSplitAcrossReads()
    {
        var scanner = new FormFieldNameScanner();
        Assert.True(scanner.Append("Displ"u8));
        Assert.True(scanner.Append("ayName"u8));
        Assert.Equal(FormField.DisplayName, scanner.Complete());
    }

    [Fact]
    public void UnknownNameIsRejectedOnItsFirstChunk() => Assert.False(new FormFieldNameScanner().Append("XDisplay"u8));

    [Fact]
    public void LongGarbageIsRejectedWithoutBuffering()
    {
        var scanner = new FormFieldNameScanner();
        Assert.True(scanner.Append("Pass"u8));
        Assert.False(scanner.Append(new byte[10_000]));
    }

    [Fact]
    public void AValidPrefixThatEndsEarlyIsUnknown()
    {
        var scanner = new FormFieldNameScanner();
        Assert.True(scanner.Append("Remember"u8));
        Assert.Equal(FormField.Unknown, scanner.Complete());
    }

    [Theory]
    [InlineData("", "", true)]      // every name starts with nothing
    [InlineData("Email", "", true)] // an exact match counts
    [InlineData("Emai", "l", true)]
    [InlineData("Email", "x", false)]
    [InlineData("email", "", false)] // case-sensitive: this enum doesn't set ParseCaseSensitive = false
    public void Semantics(string prefix, string next, bool expected) =>
        Assert.Equal(expected, FormField.IsValidPrefixFancy(System.Text.Encoding.UTF8.GetBytes(prefix), System.Text.Encoding.UTF8.GetBytes(next)));

    [Fact]
    public void IgnoreCaseOverload() => Assert.True(FormField.IsValidPrefixFancy("EMAI"u8, "L"u8, ignoreCase: true));
}
