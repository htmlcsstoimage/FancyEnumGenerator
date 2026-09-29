# FancyEnumGenerator

A Roslyn incremental source generator that turns a plain C# enum into a fast, allocation-conscious set of extension members — `ToString`, `Parse`/`TryParse`, flags handling, and arbitrary per-member field mappings — computed entirely at compile time.

## Why

- **Less boilerplate.** No more hand-written `switch` statements that map each member to a label, a database value
  or an icon, and a second one that maps it back, all kept in sync by hand. Declare the value on the member as an
  attribute and the generator writes both directions. Add a member and there's no `case` to forget.
- **Fast.** Generated `ToStringFancy`/`TryParseFancy` are plain `switch` expressions over the underlying primitive, with
  no reflection and no boxing. In the [benchmarks](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/benchmarks.md) on .NET 10:
  - Parsing a name takes 3.6 ns against `Enum.Parse`'s 46 ns on a 25-member enum, and 7.5 ns against 264 ns on a
    200-member one. A miss costs the same as a hit.
  - `ToStringFancy()` returns a string literal: under 1 ns and nothing allocated, where `ToString()` takes 6.9 ns and 24 B.
  - Parsing from a `[Description]` takes 2.8 ns, against 6.1 µs and 3.4 KB for the usual reflection.
  - Nothing is built at runtime by default: no static caches and no first-call cost ([Footprint](#footprint)).
- Works with public and internal enums, nested types, `[Flags]` enums, and any underlying numeric type.

## Installation

```bash
dotnet add package FancyEnumGenerator
```

That's the only package reference you need — it bundles both the generator and the attribute types it reads.

## Getting started

```csharp
using FancyEnumGenerator.Attributes;

[FancyEnum]
public enum Fruit
{
    Unknown = 0,
    Apple = 1,
    Banana = 2,
    Cherry = 3,
}
```

That's it. You now have:

```csharp
Fruit.Apple.ToStringFancy();               // "Apple"
Fruit.TryParseFancy("Banana", out var f);  // true, f == Fruit.Banana
Fruit.Apple.IsUnknown;                     // false
((Fruit)7).IsUnknown;                      // true
Fruit.Unknown.IsUnknown;                   // true
Fruit.Values;                              // every non-Unknown member (Apple, Banana, Cherry)
```

`[FancyEnum]` enforces two rules by default (both can be relaxed — see [Settings](#settings)):
- **Exactly one member named `Unknown` (case-insensitive) with value `0`.** Your never-fails fallback for parsing and out-of-range values.
- **Numeric values must be contiguous** (0, 1, 2, 3, ...). Needed for the generator's fastest lookup paths.

## Basic implementation

### Field mappings

Attach arbitrary per-member data with `[FancyEnumMember]`:

```csharp
[FancyEnum]
public enum Fruit
{
    Unknown = 0,

    // SortOrder is independent of the numeric value: here Banana sorts first, without renumbering the enum.
    [FancyEnumMember("Label", "apple")]
    [FancyEnumMember<int>("SortOrder", 20)]
    Apple = 1,

    [FancyEnumMember("Label", "banana")]
    [FancyEnumMember<int>("SortOrder", 10)]
    Banana = 2,
}
```

generates:

```csharp
Fruit.Apple.Label;       // "apple" (string)
Fruit.Apple.SortOrder;   // 20 (int)
Fruit.Banana.SortOrder;  // 10
```

`FancyEnumMember` (no type argument) is shorthand for string values; `FancyEnumMember<T>` works for any attribute-legal type (`bool`, numeric types, `char`, `string`, enums, `Type`).

### Fallback behavior for missing mappings

If a member doesn't set a field, `[FancyEnumMemberMappingSettings]` on the enum controls what happens:

```csharp
[FancyEnum]
[FancyEnumMemberMappingSettings("Label", NotDefined = FancyEnumMemberFallbackOption.NameOfLower)]
[FancyEnumMemberMappingSettings<int>("SortOrder", NotDefined = -1)]
public enum Fruit
{
    Unknown = 0,
    Apple = 1,   // Label falls back to "apple" (NameOfLower), SortOrder falls back to -1
}
```

Settings available per field: `NotDefined` (fallback for a member that never set this field — `Skip`/`NameOf`/`NameOfLower`/`NameOfUpper` for the non-generic string attribute, or a literal default value for the generic `<T>` one), `NotMatched` (the fallback expression used when a *runtime value* doesn't match any known member), `ReturnNullOnNotMatched` / `ThrowOnNotMatched`, `ParseFrom` (also generate `TryParseFrom_{Field}`, parsing by this field's values instead of the member name), `ParseCaseSensitive`, `CreateTryFormat`, `IncludeUtf8Value` (also generate a `ReadOnlySpan<byte>`-returning `{Field}Bytes` property, string fields only). Each is described in the [settings reference](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/settings.md#field-mapping-settings).

### Flags enums

```csharp
[Flags]
[FancyEnum]
public enum Permission
{
    Unknown = 0,
    Read = 1,
    Write = 2,
    Execute = 4,

    [FancyEnumMemberSettings(ExcludeFromValues = true)]
    All = Read | Write | Execute,
}
```

`(Permission.Read | Permission.Write).HasFlagFancy(Permission.Read)`, `ListFlagMembers` (decomposes a value into its atomic flags, .NET 8+), and `ToStringFancy(separator: '|')` all work out of the box. `[FancyEnumMemberSettings(ExcludeFromValues = true)]` keeps a composite value like `All` out of `Values`/`AsSpan` and out of the "not a single bit" warning — use it on any combined/derived value.

`IsUnknown` stays strict for flags, as for any enum: it's `true` for anything that isn't a declared member, including combinations like `Read | Write`. `IsUnknownAndNotCombined` is the flags-aware version: `false` for any combination of declared flags, and `true` only for `Unknown` itself or a value with a bit that isn't a declared flag (like `(Permission)33`). It's a single bit test. `FromUnderlying` follows the same rule for flags: `Permission.FromUnderlying(3)` returns `Read | Write`, and only a value with undeclared bits comes back as `Unknown`.

### Obsolete members

Members marked `[Obsolete]` are **excluded from generation by default**. Opt a specific enum back in with `[FancyEnum(IncludeObsolete = true)]`, or flip the default for the whole assembly (see [Settings](#settings)).

## Settings

The settings you're most likely to change:

| Setting | Default | What it does |
|---|---|---|
| `AllowNoUnknown` | `false` | Don't require an `Unknown = 0` member. (`ParseOrDefault` then replaces `ParseOrUnknown`.) |
| `AllowNonContiguous` | `false` (`true` for `[Flags]`) | Allow gaps between the numeric values. |
| `ParseCaseSensitive` | `true` | Whether parsing without an `ignoreCase` argument is case-sensitive. Field parsers inherit it. |
| `DefaultToStringBehavior` | `NameOf` | What `ToStringFancy()` returns: the name, lower- or upper-cased, a field's value, or the member's `[Description]`, `[Display]`, `[EnumMember]` or `[JsonStringEnumMemberName]`. |
| `CreateByteParsing` | `false` | Add UTF-8 (`ReadOnlySpan<byte>`) parse overloads. |
| `CreateTryFormat` | `false` | Add `TryFormat(Span<char>, out int)`, which formats into your own buffer. |
| `CreateStaticReadonlyCollection` | `false` | Cache `Values` in a static field and add a zero-copy `AsSpan`. |
| `IncludeObsolete` | `false` | Generate `[Obsolete]` members too; by default they're left out. |

Each can be set in three places. For each setting, the first of these that sets it wins:

1. On the enum: `[FancyEnum(AllowNoUnknown = true)]`
2. For the whole assembly: `[assembly: FancyEnumDefaults(ParseCaseSensitive = false)]`
3. Repo-wide, as an MSBuild property named `FancyEnum` + the setting:
   `<FancyEnumCreateByteParsing>true</FancyEnumCreateByteParsing>`

**[Full settings reference](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/settings.md):** every
setting in detail, with its effects on the generated code, interactions, and diagnostics. It also covers the rest:
`NoInlineArray`, `CreateIsValidPrefix`, `DefaultToStringCustomField`, the assembly-only `GenerateParseMethods` and
`UseGeneratedFileSuffix`, and the settings for field mappings, members, and member sets.

## Advanced

Deeper dives live in [`docs/`](https://github.com/htmlcsstoimage/FancyEnumGenerator/tree/main/docs):

- [Settings reference](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/settings.md): every setting in detail.
- [How it works](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/how-it-works.md): how the parsers pick a strategy (and the measurements behind each choice),
  what the rest of the generated code does, and how the generator stays incremental.
- [`IsValidPrefixFancy`](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/is-valid-prefix.md): rejecting unknown names mid-stream, with a form-decoding example.
- [Benchmarks](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/benchmarks.md): full results against the BCL, NetEscapades.EnumGenerators and Enums.NET.

### `FancyEnumMemberSet` — bundle several fields into one attribute

Instead of stacking several `[FancyEnumMember]`/`[FancyEnumMember<T>]` attributes on every member, define the shape once as a plain attribute class:

```csharp
[FancyEnumMemberSet]
public sealed class FruitMetadataAttribute : Attribute
{
    public string? Label { get; set; }

    [FancyEnumMemberSetItem(Name = "SortOrder", DefaultValue = -1)]
    public int Order { get; set; }
}
```

and apply it once per member:

```csharp
[FancyEnum]
public enum Fruit
{
    Unknown = 0,

    [FruitMetadata(Label = "apple", Order = 20)]
    Apple = 1,

    // No FruitMetadata at all - Label and SortOrder both fall back per their own settings.
    Banana = 2,
}
```

`Fruit.Apple.Label` and `Fruit.Apple.SortOrder` are generated exactly as if you'd written the equivalent `[FancyEnumMember]`s directly. A member that never applies the shape attribute at all behaves identically, per field, to one that applies it but omits that property — both fall through to `DefaultValue`/the field's normal fallback settings.

The enum still needs `[FancyEnum]`; using a member-set attribute on an enum without it reports `HENUM016` (a warning), since nothing is generated for it.

Every public property counts, including inherited ones, and each must be settable by some member: its type has to be one C# allows as an attribute argument (a primitive, `string`, `object`, `Type`, or an enum — no `int?`, `DateTime` or arrays), and a read-only property needs a constructor parameter mapped to it. Anything else is `HENUM015`; mark it `[FancyEnumMemberSetItem(Ignore = true)]` if the class uses it for something else. Each member-set class is validated once, where it's declared, whether or not an enum uses it yet.

Per-property controls via `[FancyEnumMemberSetItem]`:

| Property | What it does |
|---|---|
| `Name` | Override the generated field name (defaults to the property's own name). |
| `Ignore` | Excludes the property entirely — it's not a mapped field (e.g. a property the shape uses for something else). |
| `DefaultValue` | Fallback value for a member that didn't set this property. Must match the property's own type exactly — a mismatch reports `HENUM012` and the default is dropped, not silently miscompiled. |
| `NotMatched` | Value returned when no mapping applies. Same type rule (and `HENUM012`) as `DefaultValue`. |
| `ReturnNullOnNotMatched`, `ThrowOnNotMatched`, `ParseFrom`, `ParseCaseSensitive`, `CreateTryFormat`, `IncludeUtf8Value` | Same meaning as on `[FancyEnumMemberMappingSettings]`. |

### Read-only properties and constructors

A `[FancyEnumMemberSet]` property doesn't have to be settable. A read-only property's value can only come from a constructor parameter:

```csharp
[FancyEnumMemberSet]
public sealed class CssClassAttribute : Attribute
{
    public string ClassName { get; }

    public CssClassAttribute(string className) => ClassName = className;
}
```

```csharp
[CssClass("btn-primary")]
Apple = 1,
```

The parameter `className` is matched to the property `ClassName` automatically (name, case-insensitive, and type). When a parameter's name doesn't line up, mark it explicitly:

```csharp
public CssClassAttribute([FancyEnumConstructorMapping(nameof(ClassName))] string css) => ClassName = css;
```

Every public constructor with parameters on a member-set shape is validated this way at compile time (`HENUM013` if a parameter resolves to nothing), regardless of whether any enum currently uses that overload.

**Important limitation**: the generator reads the raw constructor argument exactly as written at the call site — it never actually *runs* the constructor (a source generator can't execute arbitrary code at compile time). If your constructor transforms the value before assigning it:

```csharp
public OrderAttribute(int order) => Order = order + 1; // the generator sees the raw `order`, never `order + 1`
```

...document that for whoever uses your shape. Constructors that do plain assignment keep the generated field matching what a reader would expect.

### Adding your own members

Each enum gets one generated class, `{Enum}FancyEnumExtensions`, and it's `partial`, so you can add your own members
to it. They then sit next to the generated ones and are reachable through the same `using`, with no extra import. Put the part in the enum's namespace, with the
same accessibility as the enum:

```csharp
public static partial class FruitFancyEnumExtensions
{
    extension(Fruit fruit)
    {
        public bool IsYellow => fruit == Fruit.Banana;
        public string Shout => fruit.ToStringFancy().ToUpperInvariant(); // can build on the generated members
    }
}
```

Besides the extension members, the class holds compile-time constants (`FruitFancyEnumExtensions.Length`,
`LongestCharLength`, ...) for where C# requires a constant, such as `const` fields, attribute arguments and `case`
labels. They're the same values as `Fruit.Length` and friends, which as extension members can't be constants. Nothing
is generated under the conventional `{Enum}Extensions` name, so your own `FruitExtensions` class never clashes with
generated code. For a nested enum, the enclosing types' names are joined with `_`, as in
`Outer_NestedFancyEnumExtensions`.

### Computed values via `StaticMethodName`/`StaticMethodSource`

For a value that isn't a compile-time constant:

```csharp
[FancyEnumMember<IconName>("Icon", StaticMethodName = nameof(Icons.Apple), StaticMethodSource = typeof(Icons))]
Apple = 1,
```

references a static readonly field or parameterless static method instead of a literal — the generated property reads/calls it directly. (Skips `TryFormat`/`ParseFrom` for that field, since those require a compile-time-known string value.)

### Diagnostics

| ID | Meaning |
|---|---|
| HENUM001 | Missing the required `Unknown = 0` member (`AllowNoUnknown` to relax). |
| HENUM002 | Non-contiguous numeric values (`AllowNonContiguous` to relax). |
| HENUM003 | Invalid mapping: duplicate settings for a field, or a field mapped to two different return types. |
| HENUM004 | The same numeric value declared by two members. |
| HENUM005 | `DefaultToStringBehavior = CustomFieldRequired` but a member never mapped that field. |
| HENUM006 (warning) | Two members produce the same parser token; parsing picks one. |
| HENUM007 | The enum's containing type is generic or not accessible from its namespace — extensions can't be generated. |
| HENUM008 | `CreateTryFormat` requested but unavailable (non-string field, or a `StaticMethodSource` value). |
| HENUM009 | `ParseFrom` requested but unavailable (non-string field, a `StaticMethodSource` value, or no string values to parse). |
| HENUM010 (warning) | A `[Flags]` member's value isn't zero or a single bit — use `ExcludeFromValues` for intentional composites. |
| HENUM011 | `IncludeUtf8Value` requested but unavailable (non-string field, or a value computed at runtime). |
| HENUM012 | A `FancyEnumMemberSetItem.DefaultValue` or `NotMatched` doesn't match its property's type; the value is dropped. |
| HENUM013 | A member-set constructor parameter doesn't resolve to any property; add `FancyEnumConstructorMapping`. |
| HENUM014 | A mapped field would generate a member whose name FancyEnum or `System.Enum` already uses (e.g. `Length`, `Values`, `ToString`), or that another field also generates. |
| HENUM015 | A member-set property can never be set: its type can't be an attribute argument, or it's read-only with no constructor parameter mapped to it. |
| HENUM016 (warning) | An enum uses a member-set attribute but has no `[FancyEnum]`, so nothing is generated for it. |

## Examples

The [examples](https://github.com/htmlcsstoimage/FancyEnumGenerator/tree/main/examples/FancyEnumGenerator.Examples) project has runnable, checked-in before/after: hand-written enums next to the exact code the generator produces for them (under [`examples/FancyEnumGenerator.Examples/Generated/`](https://github.com/htmlcsstoimage/FancyEnumGenerator/tree/main/examples/FancyEnumGenerator.Examples/Generated)), covering every feature above. [`LargeEnum.cs`](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/examples/FancyEnumGenerator.Examples/LargeEnum.cs) is a 90-member `HttpHeader` enum, parsed case-insensitively from text or UTF-8, if you want to see what the generated code looks like at scale.

## Performance

A few highlights, from a full-precision BenchmarkDotNet run on .NET 10 (Apple M1 Max). The complete, unedited reports,
and which libraries each one compares, are in [docs/benchmarks.md](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/benchmarks.md).

| | BCL | FancyEnum | NetEscapades | Enums.NET |
|---|--:|--:|--:|--:|
| `ToString` | 6.9 ns, 24 B | <1 ns | <1 ns | <1 ns |
| Parse, hit (25 members) | 46.0 ns | 3.6 ns | 36.3 ns | 14.9 ns |
| Parse, hit (200 members) | 263.7 ns | 7.5 ns | 227.4 ns | 14.2 ns |
| Parse, miss (200 members) | 485.5 ns | 7.3 ns | 351.3 ns | 13.1 ns |
| Parse, ignoring case | 56.6 ns | 8.4 ns | 47.5 ns | 17.1 ns |
| Parse from `[Description]` | 6.1 µs, 3.4 KB (reflection) | 2.8 ns | | |

Nothing here allocates except where noted.

## Footprint

- **No runtime state by default.** Generated members are `switch` expressions over constants and string literals, so an
  enum adds no static fields and no heap allocations, even on first use. The one exception is the opt-in
  `CreateStaticReadonlyCollection`: it adds one lazily filled buffer of N × the enum's size, or a plain array when
  combined with `NoInlineArray`.
- **Code size, in exchange.** The generated code is compiled into your assembly. In a release build that's about 6.5 KB
  for a 25-member enum with default settings, about 11.5 KB with UTF-8 parsing, `TryFormat` and the cached collection
  all on, and about 38 KB for a 200-member enum. Much of that is parsing: exact and case-insensitive lookups each get
  their own set of `switch` cases. On top of that comes about 1.5 KB of shared parsing helpers, once per assembly. The
  BCL and reflection-based libraries add no code to your assembly; instead they build their caches at runtime.

## Acknowledgements

- The parser's approach, which switches on input length and then compares ASCII tokens packed into `ulong` constants (with upper-cased constants for case-insensitive matching), is inspired by [StackExchange.Redis's `AsciiHash`](https://github.com/StackExchange/StackExchange.Redis/blob/main/eng/StackExchange.Redis.Build/AsciiHash.md).
- [NetEscapades.EnumGenerators](https://github.com/andrewlock/NetEscapades.EnumGenerators) by Andrew Lock was the inspiration for generating fast enum helpers with a source generator in the first place, and is well worth a look.

## License

MIT — see [LICENSE](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/LICENSE).
