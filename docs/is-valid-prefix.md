# `IsValidPrefixFancy`: rejecting unknown names mid-stream

`IsValidPrefixFancy` answers one question: *could the UTF-8 bytes I've read so far still turn out to be one of this
enum's names?* It's for decoders that read input incrementally, from a socket or a `PipeReader`, and want to give up
on a name they don't recognize as soon as possible, instead of buffering all of it first.

Most enums never need this, so it's opt-in:

```csharp
[FancyEnum(CreateIsValidPrefix = true, CreateByteParsing = true)]
public enum FormField { Unknown, Email, DisplayName, Password, RememberMe }
```

(`CreateByteParsing` isn't required for `IsValidPrefixFancy`. The example below also parses the finished name from UTF-8
bytes, which is what `CreateByteParsing` adds.)

It generates:

```csharp
public static bool IsValidPrefixFancy(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> next);
public static bool IsValidPrefixFancy(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> next, bool ignoreCase);
```

It returns `true` if some parseable string of the enum (a member name, or its `ToStringFancy()` text) starts with
`prefix` followed by `next`. An exact match counts, and so does an empty input, since every name starts with nothing.
The input is taken as two pieces, `prefix` and `next`, because streaming input arrives in chunks: the bytes buffered so
far and the bytes just read can be checked together without first copying them into one buffer.

## Example: decoding form fields from a stream

An `application/x-www-form-urlencoded` body (`Email=a%40b.com&RememberMe=true`) is a sequence of `name=value` pairs.
When you read it from a stream, a field name can be split across two reads. You also can't trust its length: a
client can send a 10 MB "name". Buffering whole names before looking them up means buffering whatever the client
sends.

Checking each chunk as it arrives bounds that buffer by the longest known field name:

```csharp
/// <summary>Collects one form field name, chunk by chunk, giving up as soon as it can't be a known field.</summary>
public sealed class FormFieldNameScanner
{
    // Every valid prefix is at most as long as the longest name, so this never overflows.
    // (LongestCharLength counts chars; for ASCII names that equals the byte count.)
    private readonly byte[] _buffer = new byte[FormField.LongestCharLength];
    private int _length;

    /// <summary>
    /// Adds the next chunk of the name: the bytes up to '=' or the end of the read.
    /// Returns false once no known field starts this way; skip the rest of the pair (up to the next '&').
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
```

If a read ends in the middle of `DisplayName`, say after `Displ`, `Append("Displ"u8)` returns `true` and the next read
continues with `Append("ayName"u8)`. A name like `XDisplay` fails on its first chunk, so the decoder skips to the next
`&` having buffered nothing. [`IsValidPrefixTests`](../tests/FancyEnumGenerator.RuntimeTests/IsValidPrefixTests.cs)
exercises exactly this scanner.

## Details

- **Case:** the two-argument overload follows the enum's `ParseCaseSensitive` setting; the `ignoreCase` overload
  overrides it. Case-insensitive matching folds ASCII letters only.
- **Cost:** it checks each name in turn, so it's linear in the number of names. That's fine for the small sets it's
  meant for (form fields, header names, command verbs), but a poor fit for a very large enum on a hot path.
- **Scope:** only the enum's own names and `ToStringFancy()` strings are considered, not the `TryParseFrom_*` fields.
