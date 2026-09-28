# FancyEnumGenerator

A Roslyn incremental source generator that turns a plain C# enum into a fast, allocation-conscious set of extension members — `ToString`, `Parse`/`TryParse`, flags handling, and arbitrary per-member field mappings — computed entirely at compile time.

## Why

- No runtime reflection, no `Enum.GetValues`/`Enum.Parse` boxing — generated `ToStringFancy`/`TryParseFancy` are plain `switch` expressions over the underlying primitive.
- Per-member metadata (a display label, a database column value, a UI icon, ...) declared right on the enum member as attributes, with all the enum-to-metadata plumbing generated for you.
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

Settings available per field: `NotDefined` (fallback for a member that never set this field — `Skip`/`NameOf`/`NameOfLower`/`NameOfUpper` for the non-generic string attribute, or a literal default value for the generic `<T>` one), `NotMatched` (the fallback expression used when a *runtime value* doesn't match any known member), `ReturnNullOnNotMatched` / `ThrowOnNotMatched`, `ParseFrom` (also generate `TryParseFrom_{Field}`, parsing by this field's values instead of the member name), `ParseCaseSensitive`, `CreateTryFormat`, `IncludeUtf8Value` (also generate a `ReadOnlySpan<byte>`-returning `{Field}Bytes` property, string fields only).

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

### Obsolete members

Members marked `[Obsolete]` are **excluded from generation by default**. Opt a specific enum back in with `[FancyEnum(IncludeObsolete = true)]`, or flip the default for the whole assembly (see [Settings](#settings)).

## Settings

### Per-enum: `[FancyEnum]`

| Property | Default | What it does |
|---|---|---|
| `AllowNoUnknown` | `false` | Skip the "must have an `Unknown = 0` member" requirement. |
| `AllowNonContiguous` | `false` (`true` for `[Flags]` enums) | Skip the "values must be contiguous" requirement. |
| `IncludeObsolete` | `false` | Whether `[Obsolete]`-marked members are generated at all. |
| `NoInlineArray` | `false` | Don't generate the .NET 8+ inline-array struct. `Values`/`AsSpan` are then only generated when `CreateStaticReadonlyCollection` is also set (backed by a plain array), and flags enums lose `ListFlagMembers`. |
| `CreateStaticReadonlyCollection` | `false` | Cache `Values` in a static field (built once, lazily, on first access) and also generate `AsSpan`. When this forces a plain-array representation (older targets, or `NoInlineArray = true`), `Values` is typed `IReadOnlyList<T>`, not `T[]`, so callers can't mutate the shared static backing storage. By default `Values` is rebuilt fresh on every access instead (cheap — a value-type copy, not a heap allocation — but with no stable backing store, so no `AsSpan`). |
| `DefaultToStringBehavior` | `NameOf` | How `ToStringFancy()` renders a member with no explicit mapping: `NameOf`/`NameOfLower`/`NameOfUpper`; `CustomFieldRequired`/`CustomFieldFallback` (use `DefaultToStringCustomField`'s mapped value); or read a well-known attribute, falling back to the member's name when it's absent — `DescriptionAttribute` (`[Description("...")]`), `DisplayAttribute` (`[Display(Name = "...")]` — note this is `System.ComponentModel.DataAnnotations.DisplayAttribute`, *not* `DisplayNameAttribute`, which doesn't support fields and so can't be applied to an enum member at all), `EnumMemberAttribute` (`[EnumMember(Value = "...")]`), or `JsonStringEnumMemberNameAttribute` (`[JsonStringEnumMemberName("...")]`). |
| `DefaultToStringCustomField` | `null` | The field name `CustomFieldRequired`/`CustomFieldFallback` reads from. |
| `CreateTryFormat` | `false` | Generate `TryFormat(Span<char>, out int)` for the default ToString value. |
| `CreateByteParsing` | `false` | Generate UTF-8 `ReadOnlySpan<byte>` overloads of `TryParseFancy`/`ParseOr*`. |
| `CreateIsValidPrefix` | `false` | Generate `IsValidPrefixFancy`, for streaming decoders that need to know early whether the bytes read so far could still be a member name. Niche; see [docs/is-valid-prefix.md](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/is-valid-prefix.md). |
| `ParseCaseSensitive` | `true` | Case sensitivity for `TryParseFancy` against member names, and for any field parser (`TryParseFrom_*`) that doesn't set its own `ParseCaseSensitive`. |

### Assembly-wide: `[assembly: FancyEnumDefaults(...)]`

Every property above (plus two more that have no per-enum equivalent) can be set once for the whole assembly:

```csharp
[assembly: FancyEnumDefaults(AllowNoUnknown = true, ParseCaseSensitive = false)]
```

| Property | Default | What it does |
|---|---|---|
| `GenerateParseMethods` | `true` | Generate `TryParseFancy`/`ParseOr*`/`TryParseFrom_*` at all. Set `false` to drop all parsing code (codegen-size lever). |
| `UseGeneratedFileSuffix` | `true` | Whether generated files get the `.g.cs` suffix (see below). |

Precedence, highest to lowest:
1. The specific enum's own `[FancyEnum(...)]` property, if set.
2. `[assembly: FancyEnumDefaults(...)]`.
3. The matching MSBuild property (see below).
4. The library's hardcoded default (the tables above).

### MSBuild properties

The same settings are also available from your `.csproj` or `Directory.Build.props` — useful for a repo-wide default without touching source:

```xml
<PropertyGroup>
  <FancyEnumAllowNoUnknown>true</FancyEnumAllowNoUnknown>
  <FancyEnumParseCaseSensitive>false</FancyEnumParseCaseSensitive>
</PropertyGroup>
```

Every property name is `FancyEnum` + the property name from the tables above (`FancyEnumAllowNoUnknown`, `FancyEnumIncludeObsolete`, `FancyEnumDefaultToStringBehavior`, `FancyEnumGenerateParseMethods`, `FancyEnumUseGeneratedFileSuffix`, ...) — wired up automatically the moment you reference the package, no extra setup required.

### Generated file naming

By default, generated files use the `.g.cs` suffix (e.g. `Fruit.FancyEnum.g.cs`) — the convention several tools (`dotnet format`, some `.gitignore`/`.editorconfig` setups) already treat as "generated, skip me." Set `UseGeneratedFileSuffix = false` (assembly attribute or MSBuild property) for plain `.cs` instead.

## Advanced

Deeper dives live in [`docs/`](https://github.com/htmlcsstoimage/FancyEnumGenerator/tree/main/docs):

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

A few highlights. Full results, and which libraries each row compares, are in [docs/benchmarks.md](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/benchmarks.md).
Measured with a BenchmarkDotNet ShortRun on .NET 10 (Apple M1 Max).

| | BCL | FancyEnum | NetEscapades | Enums.NET |
|---|--:|--:|--:|--:|
| `ToString` | 7.9 ns, 24 B | <1 ns | <1 ns | <1 ns |
| Parse, hit (25 members) | 45.3 ns | 3.9 ns | 37.2 ns | 11.3 ns |
| Parse, hit (200 members) | 275.4 ns | 8.0 ns | 228.3 ns | 14.3 ns |
| Parse, miss (200 members) | 488.0 ns | 8.5 ns | 413.3 ns | 13.5 ns |
| Parse, ignoring case | 56.8 ns | 8.4 ns | 50.3 ns | 19.0 ns |
| Parse from `[Description]` | 6.1 µs, 3.4 KB (reflection) | 3.2 ns | | |

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
