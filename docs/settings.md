# Settings reference

Every FancyEnum setting in detail: its default, what it adds to or removes from the generated code, how it
interacts with other settings, and which diagnostics it can raise. The [README](../README.md#settings) has the short
version.

- [Where settings come from](#where-settings-come-from)
- [Enum settings](#enum-settings): the rules, formatting, parsing, collections
- [Assembly-wide only](#assembly-wide-only)
- [Field mapping settings](#field-mapping-settings)
- [Member settings](#member-settings)
- [Member-set settings](#member-set-settings)

## Where settings come from

Enum settings can be set in three places. For each setting, the first of these that sets it wins:

1. **The enum's own attribute:** `[FancyEnum(AllowNoUnknown = true)]`
2. **An assembly-wide default:** `[assembly: FancyEnumDefaults(AllowNoUnknown = true)]`
3. **An MSBuild property**, in a `.csproj` or a shared `Directory.Build.props`:
   ```xml
   <PropertyGroup>
     <FancyEnumAllowNoUnknown>true</FancyEnumAllowNoUnknown>
   </PropertyGroup>
   ```

If none of them sets it, the library default applies.

Only settings that are actually written count. `[assembly: FancyEnumDefaults(CreateTryFormat = true)]` changes
`CreateTryFormat` and nothing else: every other setting still falls through to MSBuild and then to the default. The
same goes for `[FancyEnum]` itself, so an enum with a bare `[FancyEnum]` takes everything from the levels below.

**MSBuild properties** are always `FancyEnum` followed by the setting's name: `FancyEnumAllowNoUnknown`,
`FancyEnumDefaultToStringBehavior`, and so on. They're available as soon as the package is referenced, with no extra
setup. Values are parsed leniently: `true`/`True`, and enum values by name in any case (`NameOfLower`,
`nameoflower`). An empty value counts as unset.

Field, member, and member-set settings have no assembly or MSBuild level; they're set only on their own attributes.
The one exception is a field's `ParseCaseSensitive`, which inherits the enum's value when it isn't set.

## Enum settings

Set on `[FancyEnum(...)]`, `[assembly: FancyEnumDefaults(...)]`, or MSBuild.

### The rules: `AllowNoUnknown`, `AllowNonContiguous`, `IncludeObsolete`

#### `AllowNoUnknown`

**Default:** `false`

By default an enum must declare a member named `Unknown` (matched case-insensitively) with value `0`, or the build
fails with **HENUM001**. That member is the answer for anything that isn't a real member: failed parses
(`ParseOrUnknown`), undeclared numbers (`FromUnderlying(99)`), and so on.

With `AllowNoUnknown = true` there's no such member, and some generated API changes shape:

| With an Unknown member | Without one |
|---|---|
| `IsUnknown`, `ValueOrDefaultIfUnknown`, `AsUnderlyingNonUnknown` (and, for flags, `IsUnknownAndNotCombined`) | Not generated |
| `ParseOrUnknown(...)` falls back to `Unknown` | `ParseOrDefault(...)`, falling back to the lowest-valued member |
| `FromUnderlying(99)` returns `Unknown` | Returns `default` (the member with value `0`, if any) |
| `Values` and `FirstNonUnknown` exclude it | Include every member |

Flags enums usually have a `None = 0` rather than an `Unknown`, and set this.

#### `AllowNonContiguous`

**Default:** `false`, or `true` for `[Flags]` enums

By default the enum's values must be contiguous (0, 1, 2, 3, ...), or the build fails with **HENUM002**. Contiguous
values let `FromUnderlying` and `IsUnknown` be a range check rather than a `switch`. `AllowNonContiguous = true`
accepts gaps, and those members become a `switch` over the declared values, which is still fast, just not the
fastest form.

`[Flags]` enums are non-contiguous by construction (1, 2, 4, ...), so for them the default is already `true`. Their
`FromUnderlying` doesn't use the declared-values `switch` for combinations anyway: any combination of declared flags
is accepted with a single bit test. An
explicit `AllowNonContiguous = false` at any level turns the check back on.

#### `IncludeObsolete`

**Default:** `false`

Members marked `[Obsolete]` are left out of all generated code by default: they don't format (`ToStringFancy()`
returns `""`), don't parse, aren't in `Values`, and count as unknown (`IsUnknown` is `true`, and `FromUnderlying`
returns `Unknown`). That's usually what you want when a member is deprecated, but it's a behavior change the day you
add `[Obsolete]`. Obsoleting a member in the middle of the enum doesn't trip the contiguity rule: that's judged on every
declared value, and the generated code simply uses a `switch` where the gap is.

`IncludeObsolete = true` generates them like any other member. The generated code suppresses the obsolete
warnings it would otherwise cause (CS0612, CS0618), so your build stays clean.

### Formatting: `DefaultToStringBehavior`, `DefaultToStringCustomField`, `CreateTryFormat`

#### `DefaultToStringBehavior`

**Default:** `NameOf`

What `ToStringFancy()` returns for each member. Whatever it returns, `TryParseFancy` also accepts it, alongside the
member's name.

| Value | `ToStringFancy()` returns |
|---|---|
| `NameOf` | The member's name, exactly as declared (emitted as `nameof(...)`, so it follows renames) |
| `NameOfLower` / `NameOfUpper` | The name, lower- or upper-cased (invariant culture) |
| `CustomFieldRequired` | The member's value for the string field named by `DefaultToStringCustomField`. A member without one is an error (**HENUM005**) |
| `CustomFieldFallback` | The same, falling back to the name for members without one |
| `DescriptionAttribute` | The member's `[Description("...")]`, else its name |
| `DisplayAttribute` | The member's `[Display(Name = "...")]`, else its name |
| `EnumMemberAttribute` | The member's `[EnumMember(Value = "...")]`, else its name |
| `JsonStringEnumMemberNameAttribute` | The member's `[JsonStringEnumMemberName("...")]` (.NET 9+), else its name |

`DisplayAttribute` means `System.ComponentModel.DataAnnotations.DisplayAttribute`. `DisplayNameAttribute` can't be
applied to an enum member at all.

For a value that isn't a declared member, `ToStringFancy()` returns `""`, unlike `Enum.ToString()`, which formats
the number.

#### `DefaultToStringCustomField`

**Default:** none

The field that `CustomFieldRequired` and `CustomFieldFallback` read from. It has to be a string field with constant
values, `[FancyEnumMember("Name", "...")]`; other behaviors ignore it. This is what makes a wire name (`"Content-Type"`)
the formatted and parseable form of a member (`ContentType`): see the
[`HttpHeader` example](../examples/FancyEnumGenerator.Examples/LargeEnum.cs).

#### `CreateTryFormat`

**Default:** `false`

Adds `TryFormat(Span<char> destination, out int charsWritten)`, which writes `ToStringFancy()` into your buffer
without allocating. `LongestCharLength` is computed at compile time and is always a large enough buffer. For a flags
enum it also takes the same optional `separator` as `ToStringFancy`, and writes a combination that isn't a declared
member (like `Read|Write`) straight into the buffer; a buffer that's too small fails before anything is written. Fields have their own version (see [`CreateTryFormat` for fields](#createtryformat-and-includeutf8value)).

### Parsing: `ParseCaseSensitive`, `CreateByteParsing`, `CreateIsValidPrefix`

#### `ParseCaseSensitive`

**Default:** `true`

Whether the parse overloads without an `ignoreCase` parameter match case-sensitively: `TryParseFancy(input, out
result)` and `ParseOrUnknown(input)`. The overloads that take `ignoreCase` always do what they're told. Ignoring case
uses the same rules as `StringComparison.OrdinalIgnoreCase` for text; for UTF-8 input, only ASCII letters are folded.

This is also the default for the enum's field parsers (`TryParseFrom_*`) that don't set their own
`ParseCaseSensitive`.

When two different members have a name or string that differs only by case (`Alpha` and `ALPHA`), parsing ignoring
case can't tell them apart; that's reported as **HENUM006** (a warning) when the default ignores case.

#### `CreateByteParsing`

**Default:** `false`

Adds `ReadOnlySpan<byte>` (UTF-8) overloads of `TryParseFancy`, `ParseOrUnknown`/`ParseOrDefault`, and every
`TryParseFrom_*`. They parse bytes straight off a socket or out of a JSON reader, without decoding them into a string
first. See [how the UTF-8 parser works](how-it-works.md#utf-8-packed-ascii).

#### `CreateIsValidPrefix`

**Default:** `false`

Adds `IsValidPrefixFancy(prefix, next)`, which answers "could these UTF-8 bytes still become a member name?" It's for
streaming decoders that want to give up on an unknown name early instead of buffering all of it. It's niche, and
independent of `CreateByteParsing`. See [its own page](is-valid-prefix.md).

### Collections: `CreateStaticReadonlyCollection`, `NoInlineArray`

#### `CreateStaticReadonlyCollection`

**Default:** `false`

`Values` lists the declared members, except `Unknown` and any marked [`ExcludeFromValues`](#excludefromvalues). By
default it's built fresh on each access as an inline-array struct (.NET 8+), which is a value copy on the stack, not
a heap allocation, and there's no static state at all. That fits most uses: `foreach (var fruit in Fruit.Values)`.

`CreateStaticReadonlyCollection = true` keeps one copy in a static field instead, filled in on first access, and adds
`AsSpan`: a `ReadOnlySpan<T>` over that field, with no copy. Use it when you pass the members around as a span, or
read them very often.

What you get depends on the target and on `NoInlineArray`:

| | Default | `CreateStaticReadonlyCollection = true` |
|---|---|---|
| .NET 8+ | `Values`: inline array, built per access | `Values`: inline array, cached; `AsSpan` |
| Older targets, or `NoInlineArray = true` | Nothing generated | `Values`: `IReadOnlyList<T>` over a static array; `AsSpan` |

`Values` is an `IReadOnlyList<T>` rather than the array itself, so callers can't modify the shared copy.

#### `NoInlineArray`

**Default:** `false`

Stops the generator from emitting the `{Name}Array` inline-array struct on .NET 8+. Without it, `Values` and `AsSpan`
exist only with `CreateStaticReadonlyCollection` (backed by a plain array), and flags enums lose `ListFlagMembers`.
It's for when you'd rather not have the extra public struct in your assembly.

## Assembly-wide only

These have no per-enum form; set them with `[assembly: FancyEnumDefaults(...)]` or MSBuild.

#### `GenerateParseMethods`

**Default:** `true`

`false` drops every parser: `TryParseFancy`, `ParseOrUnknown`/`ParseOrDefault`, `IsValidPrefixFancy`, and every
`TryParseFrom_*`, even for fields that ask for `ParseFrom`. Parsing is the largest part of the generated code (see
[Footprint](../README.md#footprint)), so this is the setting to reach for when you only ever format enums and want
the smallest output.

#### `UseGeneratedFileSuffix`

**Default:** `true`

Generated files are named `Fruit.FancyEnum.g.cs`. Tools such as `dotnet format`, and many `.editorconfig` setups,
already treat `.g.cs` as generated and skip it. Set `false` for plain `Fruit.FancyEnum.cs`.

## Field mapping settings

A field mapping attaches a value to each member: `[FancyEnumMember("Label", "apple")]` for strings, or
`[FancyEnumMember<int>("SortOrder", 20)]` for other types. Each field becomes a property on the enum
(`Fruit.Apple.Label`). How a field behaves is set once per enum with `[FancyEnumMemberMappingSettings("Label", ...)]`,
or `[FancyEnumMemberMappingSettings<int>("SortOrder", ...)]` for non-string fields.

A field's values can be any type an attribute accepts: primitives, `string`, `Type`, and enums. They can also come
from code: `StaticMethodName` and `StaticMethodSource` name a parameterless static method or `static readonly`
field that produces the value at runtime. A field with such values can't be parsed from (**HENUM009**), formatted
with `TryFormat_*` (**HENUM008**), or turned into UTF-8 (**HENUM011**), since those all need values known at compile
time.

### What a member without a value gets

Two different situations, with separate settings:

- **`NotDefined`**: the value for a *declared member* that doesn't map this field. For string fields it's a
  fallback option: `Skip` (the default), `NameOf`, `NameOfLower`, or `NameOfUpper`. For typed fields it's a literal
  value (`NotDefined = -1`). The name options only work for string fields (**HENUM003** otherwise).
- **`NotMatched`**: the value when there's no mapping at all. That covers a value that isn't a declared member, like
  `(Fruit)99`, and members left out by `NotDefined = Skip`.

In order, the first of these that applies decides what a lookup with no mapping returns:

1. `ThrowOnNotMatched = true`: throws `ArgumentOutOfRangeException`.
2. `NotMatched = ...`: returns that value.
3. `ReturnNullOnNotMatched = true`, or the field is a reference type other than `string` (like `Type` or `object`):
   returns `null`, and the property is typed nullable.
4. Otherwise: `""` for strings, `default` for everything else.

A member mapping the same field twice, mixing types for one field, or declaring settings for a field twice is
**HENUM003**.

### `ParseFrom` and `ParseCaseSensitive`

`ParseFrom = true` adds `TryParseFrom_{Field}`, which parses the field's values (including `NotDefined` fallbacks)
back to their member: `Fruit.TryParseFrom_Label("apple", out var fruit)`. It's for string fields only
(**HENUM009**). `NotMatched` isn't something you can parse back to.

`ParseCaseSensitive` sets its case sensitivity. **When not set, it inherits the enum's
[`ParseCaseSensitive`](#parsecasesensitive)**, so an enum that ignores case by default also ignores case in its field
parsers unless a field says otherwise. When two members share a value, parsing picks one and reports **HENUM006**.

### `CreateTryFormat` and `IncludeUtf8Value`

For string fields only:

- **`CreateTryFormat = true`** adds `TryFormat_{Field}(Span<char>, out int)` and a `{Field}_LongestCharLength`
  constant for sizing the buffer (**HENUM008** on non-string fields).
- **`IncludeUtf8Value = true`** adds `{Field}Bytes`, a `ReadOnlySpan<byte>` from a `u8` literal. It suits writing
  wire names without transcoding, like the `HttpHeader` example's `NameBytes` (**HENUM011** on non-string fields or
  values computed at runtime).

### Reserved names

A field can't be named like something FancyEnum or `System.Enum` already generates or defines (`Length`, `Values`,
`IsUnknown`, `ToString`, `HasFlag`, ...). Nor can it generate the same member name as another field does (a field
`LabelBytes` next to a `Label` with `IncludeUtf8Value`). Either is **HENUM014**, and the field is skipped, so the
rest of the enum still compiles.

## Member settings

`[FancyEnumMemberSettings(...)]` on an individual enum member. You can derive your own attribute from it.

#### `ExcludeFromValues`

**Default:** `false`

Leaves the member out of `Values`, `AsSpan`, and `ListFlagMembers`, while it still formats, parses, and keeps its
field mappings. It's meant for composite flags like `All = Read | Write | Execute`. On a `[Flags]` enum it also
silences **HENUM010**, the warning for a member whose value isn't a single bit.

## Member-set settings

A member set is your own attribute class, marked `[FancyEnumMemberSet]`, whose properties each become a field; see
the [README](../README.md#fancyenummemberset--bundle-several-fields-into-one-attribute). The enum using it still
needs `[FancyEnum]` (**HENUM016** warns if it's missing). Each property can be tuned with
`[FancyEnumMemberSetItem(...)]`:

| Setting | What it does |
|---|---|
| `Name` | The field's name, instead of the property's (`[FancyEnumMemberSetItem(Name = "SortOrder")] public int Order`) |
| `Ignore` | Not a field at all: for properties the class uses for something else |
| `DefaultValue` | The value for members that don't set this property, like `NotDefined` for fields |
| `NotMatched` | As for fields |
| `ReturnNullOnNotMatched`, `ThrowOnNotMatched`, `ParseFrom`, `CreateTryFormat`, `IncludeUtf8Value` | As for fields |
| `ParseCaseSensitive` | As for fields: inherits the using enum's `ParseCaseSensitive` when not set |

`DefaultValue` and `NotMatched` must be constants of exactly the property's type: `5L`, not `5`, for a `long`. The
exceptions are that anything goes for an `object` property, and `null` for any reference type. A mismatch is
**HENUM012**, and the value is ignored.

Every property that isn't `Ignore`d has to be settable by some member, or it's **HENUM015**. That rules out a type
an attribute can't take (`DateTime`, `int?`, arrays), and a read-only property that no constructor parameter feeds.
Constructor parameters map to properties by matching name (ignoring case) and type, or explicitly with
`[FancyEnumConstructorMapping(nameof(Property))]`. A public constructor parameter that maps to nothing is
**HENUM013**. Inherited public properties count too. Each member-set class is checked once, where it's declared, so
its errors appear even before any enum uses it.

If an enum sets `[FancyEnumMemberMappingSettings]` for the same field name, that wins over the member set's
settings, for that enum only.
