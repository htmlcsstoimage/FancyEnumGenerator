# How FancyEnum generates code

This page is for anyone who wants to know *why* the generated code looks the way it does: the strategy decisions,
their thresholds, and the measurements behind them. You don't need any of it to use the library. To see the real
output, browse [`examples/FancyEnumGenerator.Examples/Generated/`](../examples/FancyEnumGenerator.Examples/Generated);
[`HttpHeader.FancyEnum.g.cs`](../examples/FancyEnumGenerator.Examples/LargeEnum.cs) shows every parser strategy at once.

## Parsing

Parsing is where most of the design work went, because it's where naive approaches are slowest: the BCL's
`Enum.TryParse` searches the names one by one, so it gets slower as the enum grows. On a 200-member enum it takes about
275 ns for a hit and 488 ns for a miss (see [benchmarks](benchmarks.md)).

### What gets parsed

`TryParseFancy` accepts two kinds of token for each member: its name, and its `ToStringFancy()` text when that's
different (a `[Description]`, a custom field, and so on). `TryParseFrom_{Field}` accepts that field's values instead.
Before any code is generated, the tokens are cleaned up:

- **Exact duplicates are removed.** When two members share a token, the non-Unknown one wins. If they're both real
  members, that's reported as a warning (HENUM006), judged by the enum's default case sensitivity.
- **Every exact spelling is kept, even when the enum ignores case by default.** A member `Te` whose wire name is `"TE"`
  keeps both tokens, so the explicit `ignoreCase: false` overload still matches each one exactly.
- **Case-insensitive lookups collapse spellings separately.** `Alpha` and `ALPHA` become one entry, keyed by the
  upper-cased form. Otherwise the generated `switch` would have duplicate `case` labels and fail to compile.

### Choosing a strategy

The strategy depends on how many tokens there are and whether the input is text (`ReadOnlySpan<char>`) or UTF-8
(`ReadOnlySpan<byte>`).

| Tokens | Text | UTF-8 |
|---|---|---|
| Fewer than 4 | Compare each token directly | Compare each token directly |
| 4 or more | Switch on length, then one small `switch` of string constants per length | Switch on length, then `switch` on the first 8 bytes packed into a `ulong` |

**Direct comparison** is `SequenceEqual` (or `Equals(..., OrdinalIgnoreCase)` when ignoring case), one token after
another. With three or fewer tokens nothing beats it, and the code reads exactly as you'd write it by hand.

### Text: length, then the compiler's string switch

For 4 or more tokens, the public method switches only on `input.Length`. For each length:

- **4 or fewer tokens:** compared right there in the `case`, since a `switch` would only add a method call.
- **More than 4:** the `case` calls a small private helper, and the helper is a plain `switch` over that length's
  tokens as string constants:

```csharp
private static bool TryParseFancy__Exact14(CharSpan input, out global::HttpHeader result) {
    switch (input) {
        case "Accept-Charset":
            result = thisEnum.AcceptCharset; return true;
        case "Content-Length":
            result = thisEnum.ContentLength; return true;
        // ...
    }
    result = default;
    return false;
}
```

We leave the matching itself to Roslyn on purpose. The C# compiler lowers a `switch` over many string constants on a
span into a check on one well-chosen character position followed by a single full comparison, so a lookup costs
about the same however many names there are. A miss usually fails the length check or the character check without
comparing any string at all.

**Why one helper per length, not one big `switch`?** One big switch was tried first and measured. On a 200-member enum
it got about 3x *slower* than the previous approach: 139 ns for a hit, and 111 ns even for a miss. Every string case
leaves a span temporary in the method's frame. Past the number of locals the JIT can track, it zero-initializes all
of them in the method prologue, on every call, whatever the input. Splitting by length keeps each method small, and
brought the same benchmark to 8 ns.

The same limit caps the inlining: at most 32 direct comparisons are inlined per length dispatch, and anything past
that gets a helper regardless of size. That covers an enum with many distinct name lengths, each holding only a few
names.

**Ignoring case** reuses the same machinery. The input is upper-cased into a stack buffer, sized to the longest token
(or a heap array if some token is absurdly long, over 256 characters). It then goes through the same length dispatch,
into helpers over upper-cased constants. Invariant upper-casing, character by character, is exactly the mapping
`StringComparison.OrdinalIgnoreCase` compares with, so `"CAFÉ"` matches `Café`, the same as on the direct path.

### UTF-8: packed ASCII

A `switch` can't match `u8` literals, because they aren't compile-time constants, so UTF-8 needs its own scheme. The
approach is inspired by
[StackExchange.Redis's `AsciiHash`](https://github.com/StackExchange/StackExchange.Redis/blob/main/eng/StackExchange.Redis.Build/AsciiHash.md).
Per input length:

1. Pack the input's first 8 bytes into a `ulong` (`hash0`), and bytes 8 to 15 into another (`hash1`) if any token is
   longer than 8.
2. `switch (hash0)` over the tokens' packed first 8 bytes, which the generator computes at compile time. The compiler
   turns a `switch` over `ulong` constants into a binary search, so about 6 comparisons to tell 40 names apart.
3. Tokens that share their first 8 bytes share a `case`, and are told apart by `hash1`, then by a direct comparison of
   anything past 16 bytes.

From `HttpHeader`, the 12-byte tokens. That includes the wire name `Content-Type` and the member names `SecFetchDest`,
`SecFetchMode`, and so on. Those four start with the same 8 bytes (`SECFETCH`, upper-cased), so they share a case:

```csharp
case 12: {
    var hash0 = FancyEnumParsingHelpers.PackAscii(input, 0, ignoreCase);
    var hash1 = FancyEnumParsingHelpers.PackAscii(input, 8, ignoreCase);
    if (ignoreCase) {
        switch (hash0) {
            case 3266321689424580419UL:                   // "CONTENT-"
                if (hash1 == 1162893652UL) { result = thisEnum.ContentType; return true; }   // "TYPE"
                break;
            case 5207098250678715731UL:                   // "SECFETCH"
                if (hash1 == 1414743364UL) { result = thisEnum.SecFetchDest; return true; }   // "DEST"
                if (hash1 == 1162104653UL) { result = thisEnum.SecFetchMode; return true; }
                if (hash1 == 1163151699UL) { result = thisEnum.SecFetchSite; return true; }
                if (hash1 == 1380275029UL) { result = thisEnum.SecFetchUser; return true; }
                break;
            // ...
        }
    }
    else { /* the same, with the exact-case constants */ }
    break;
}
```

Ignoring case costs nothing extra: the input's `a`–`z` bytes are upper-cased while packing, and compared against
constants that were upper-cased at compile time. Tokens containing non-ASCII characters can't be packed, so they're
compared directly, and case-sensitively.

### Why not a dictionary?

Enums.NET looks names up in a dictionary it builds, once per enum type, the first time you use it. That's a good
general-purpose design and very consistent: about 11–14 ns for a hit or a miss at any size. A compiled `switch` does
better on both ends. There's no runtime cache to build or keep in memory, and nothing to hash: a miss usually ends at
the length check, which costs about 2–3 ns on a 25-member enum. The price is code size in your assembly; see
[Footprint](../README.md#footprint).

### `IsValidPrefixFancy`

The opt-in prefix check ([docs](is-valid-prefix.md)) simply tests each token in turn: "does this token start with
`prefix` followed by `next`?" It's linear in the number of names. That's fine for the small sets it's meant for (form
fields, header names), and it isn't worth a cleverer structure for such a niche feature.

## Everything else

- **`ToStringFancy()`** is a `switch` expression that returns `nameof(...)` or a string literal. Literals are allocated
  once by the runtime, on a heap the GC never scans, so formatting a declared member never allocates. An undeclared
  value returns `""`.
- **Flags formatting**: a declared member, including a declared composite like `ReadWrite`, is a direct lookup. Any
  other combination first adds up the exact output length from its set bits, then writes it in a single
  `string.Create` (or a stack buffer on older targets), so it allocates only the result string; `TryFormat` writes the
  same characters straight into your buffer, allocating nothing. Flags are written from the lowest bit to the highest,
  with a signed enum's sign bit last, as `Enum.ToString()` orders them. A value with any bit that isn't a declared flag
  formats as `""`, checked against one private constant of all the declared flags OR'd together
  (`FancyFlagAllDefined`). `LongestCharLength` is computed at compile time, so a buffer of that size always fits.
- **`FromUnderlying` / `IsUnknown`**: when the values are contiguous (the default rule), these are range checks. For a
  flags enum, `FromUnderlying` first checks the value against `FancyFlagAllDefined`, so any combination of declared
  flags is returned as is in one bit test; `IsUnknown` still accepts only declared members, and
  `IsUnknownAndNotCombined` is its combination-aware counterpart.
  Otherwise they're a `switch` over the declared values. That's why contiguity is required by default: it enables the
  cheaper form.
- **Field mappings**: members that map a field to the same value share one `case`. When three or more of them have
  consecutive values, the case becomes a range pattern (`>= A and <= C`) instead of a list, so the switch stays short
  even for mostly-uniform mappings.
- **`Values`**: by default it's `ReadOnlySpan<T> Values => [A, B, C];`. With constant elements the compiler doesn't
  build an array: it stores the members as data in the assembly and returns a span over it (through
  `RuntimeHelpers.CreateSpan`), so every access is the same memory, with no allocation and no copy. Before .NET 7 that
  only works for 1-byte types; for anything wider the compiler would allocate a new array on every access, so the
  property is wrapped in `#if NET7_0_OR_GREATER` and simply doesn't exist on those targets. The opt-in `ValuesType`
  options trade this for something storable: `InlineArray` builds a fresh `[InlineArray]` struct on each access (a
  stack copy, .NET 8+), and `StaticCollection` keeps one array in a static field. Both also get an `AsSpan`. Everything
  else is `const`, so by default an enum has no static state at all. See [`ValuesType`](settings.md#valuestype).
- **Target-specific code stays in `#if` blocks.** The generator doesn't emit different code per target framework:
  the same generated file compiles everywhere, with `#if NET7_0_OR_GREATER` and `#if NET8_0_OR_GREATER` around the
  members that need those runtimes. Only the HENUM017 warning looks at the target, through the compilation's
  preprocessor symbols.

## Incremental generation

The generator runs on every keystroke in the IDE, so how cheaply it can skip unchanged work matters as much as the
code it produces.

- **Matching by attribute:** enums are found with `ForAttributeWithMetadataName`, which only looks at declarations
  carrying `[FancyEnum]`. Member-set attribute classes get their own pipeline the same way, so each is analyzed, and
  its errors reported, once, rather than again for every enum that uses it.
- **Cacheable models:** everything between the compiler's symbols and the emitted code is plain, value-equatable
  records: no symbols, no syntax nodes, no `Location` objects. After an unrelated edit, every model compares equal to
  last time's, and no code is re-emitted. The incremental tests
  ([`IncrementalTests`](../tests/FancyEnumGenerator.Tests/IncrementalTests.cs)) assert exactly that.
- **Per-compilation lookups:** attribute-type symbols and the assembly-wide `[FancyEnumDefaults]` are looked up once
  per compilation, not once per enum.
- **Analyzer, not generator, for the "forgot `[FancyEnum]`" check:** finding enums that use a member-set attribute
  without `[FancyEnum]` means inspecting every attributed enum member in the project. That runs as an analyzer
  (HENUM016), in the background and off the generation path, so it adds nothing to the per-keystroke cost.
