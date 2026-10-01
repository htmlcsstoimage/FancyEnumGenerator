# Changelog

All notable changes to FancyEnumGenerator. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/). Before 1.0, a minor version bump (0.1 → 0.2) can
include breaking changes, and each one comes with upgrade notes.

## [0.2.0] - 2026-10-01

Thanks to [the suggestion on Reddit](https://www.reddit.com/r/moderndotnet/comments/1wu7mrh/comment/pd7erju/) to return a
`ReadOnlySpan<T>` from `Values` instead of an inline array: since a span over constants is static data, nothing needs
to be copied.

### Changed

- **`Values` is now a `ReadOnlySpan<T>` by default.** The compiler stores the members as static data in your
  assembly, so `Values` no longer copies anything or allocates. This needs .NET 7+, except for enums with a 1-byte
  underlying type (`byte`, `sbyte`), which now get `Values` on every target, .NET Framework and netstandard2.0
  included. In 0.1, `Values` was a fresh `[InlineArray]` copy on every access and needed .NET 8+.
- **The new `ValuesType` setting chooses what `Values` returns**, with the same precedence as every other setting: on
  `[FancyEnum]`, on `[assembly: FancyEnumDefaults]` or as the MSBuild property `FancyEnumValuesType`. The options are
  `Span` (the default), `InlineArray` (0.1's default: a storable copy) and `StaticCollection` (a cached
  `IReadOnlyList<T>`). `InlineArray` and `StaticCollection` also generate `AsSpan`; with `Span`, `Values` already is
  the span.
- **`NoInlineArray` now only controls the `[InlineArray]` struct.** Flags enums lose `ListFlagMembers` with it, and
  `ValuesType = InlineArray` isn't available alongside it.
- **The `{Enum}Array` struct is only generated when something uses it:** flags enums, for `ListFlagMembers`, and
  `ValuesType = InlineArray`.

### Removed

- **`CreateStaticReadonlyCollection`**, on both attributes and as the MSBuild property
  `FancyEnumCreateStaticReadonlyCollection`. Use `ValuesType = FancyEnumValuesType.StaticCollection`.

### Added

- **`FancyEnumValuesType`**, the enum behind `ValuesType`.
- **HENUM017 (warning):** an explicitly chosen `ValuesType` can't be generated for the current target. That's `Span` on
  an enum wider than a byte before .NET 7, `InlineArray` before .NET 8, or `InlineArray` with `NoInlineArray`.
  `Values` is left out for that target only, so a multi-targeted project still builds. The default never warns.

### Fixed

- **An enum named `Inline` no longer breaks the build.** Its generated `InlineArray` struct shadowed the attribute alias
  in the generated file (CS0616). The attribute is now fully qualified.

### Upgrading from 0.1

Most code needs no changes: `foreach (var value in MyEnum.Values)` and `MyEnum.Values[i]` work the same with every
`ValuesType`. What does need attention:

| If your 0.1 code... | ...do this in 0.2 |
|---|---|
| Sets `CreateStaticReadonlyCollection = true` | Set `ValuesType = FancyEnumValuesType.StaticCollection`. `AsSpan` is unchanged. `Values` becomes an `IReadOnlyList<T>`, where on .NET 8+ it used to be a cached inline-array copy. |
| Sets `<FancyEnumCreateStaticReadonlyCollection>true</FancyEnumCreateStaticReadonlyCollection>` | Use `<FancyEnumValuesType>StaticCollection</FancyEnumValuesType>`. |
| Stores the default `Values` in a field, keeps it across an `await`, captures it in a lambda or uses it in an iterator | A span can't do that. Set `ValuesType = FancyEnumValuesType.InlineArray` (exactly 0.1's behavior) or `StaticCollection` (no copy at all), or call `.ToArray()`. |
| Assigns the default `Values` to a `Span<T>` | Use `ReadOnlySpan<T>`. The members are read-only data now. |
| Names the `{Enum}Array` type for a non-flags enum, e.g. `FruitArray values = Fruit.Values;` | Use `var` or `ReadOnlySpan<Fruit>`, or set `ValuesType = InlineArray` to keep the struct. |
| Uses `AsSpan` on an enum that only had it because of `CreateStaticReadonlyCollection` | Either keep it with `ValuesType = StaticCollection`, or move to the default and use `Values`, which is the span. |
| Combines `NoInlineArray = true` with `CreateStaticReadonlyCollection = true` | Use `ValuesType = StaticCollection`. Keep `NoInlineArray` only if you also want flags enums without `ListFlagMembers`. |

To keep 0.1's `Values` behavior across a whole repo while you migrate, set this in `Directory.Build.props`:

```xml
<PropertyGroup>
  <FancyEnumValuesType>InlineArray</FancyEnumValuesType>
</PropertyGroup>
```

A note on Debug builds: the JIT doesn't optimize `RuntimeHelpers.CreateSpan` in unoptimized code, so the default
`Values` allocates in a Debug build. Release builds are allocation-free.

## [0.1.2] - 2026-09-29

### Added

- **`FancyEnumMemberSetItem.NotDefined`:** a string member-set property can fall back to the member's name (`NameOf`,
  `NameOfLower`, `NameOfUpper`) for members that don't set it, like `NotDefined` on `[FancyEnumMemberMappingSettings]`.
  It works with `ParseFrom`, `CreateTryFormat` and `IncludeUtf8Value`. `DefaultValue` wins if both are set. On a
  non-string property it reports HENUM012 and is ignored.

## [0.1.1] - 2026-09-29

### Changed

- Documentation only: full benchmark results, generated from the BenchmarkDotNet reports, and refreshed performance
  numbers in the README.

## [0.1.0] - 2026-09-29

- First public release.

[0.2.0]: https://github.com/htmlcsstoimage/FancyEnumGenerator/compare/v0.1.2...v0.2.0
[0.1.2]: https://github.com/htmlcsstoimage/FancyEnumGenerator/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/htmlcsstoimage/FancyEnumGenerator/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/htmlcsstoimage/FancyEnumGenerator/releases/tag/v0.1.0
