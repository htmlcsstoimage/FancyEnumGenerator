# Benchmarks

FancyEnum against the BCL (`System.Enum`), [NetEscapades.EnumGenerators](https://github.com/andrewlock/NetEscapades.EnumGenerators)
(1.0.0-beta21) and [Enums.NET](https://github.com/TylerBrinkley/Enums.NET) (5.0.0). Source: [`benchmarks/`](../benchmarks).

```
BenchmarkDotNet v0.15.8, macOS Tahoe 26.6.1, Apple M1 Max, .NET 10.0.9 (Arm64 RyuJIT)
Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

These are **ShortRun** numbers: fine for relative comparisons, but they carry wide error bars. Anything under ~1 ns
is at the measurement floor (effectively a constant load) and is shown as `<1 ns`. Every benchmark checks during setup
that all libraries produce the same answer for its input, so no row measures different work than another.

**Which libraries appear where:** all four are compared on formatting and parsing names, which each supports natively.
Everything else is BCL vs FancyEnum, plus any library with a native API for it; nothing is given a shim (for example,
a UTF-8 transcode) just to fill in a row.

**Enums used:** *Medium* has 25 members and *Large* has 200. Their names are two words, 9–13 characters long, with many
shared prefixes and lengths ([`generate_subjects.cs`](../benchmarks/generate_subjects.cs)).

## Formatting a member name

| | Medium | Large | Allocated |
|---|--:|--:|--:|
| BCL `ToString()` | 7.9 ns | 7.5 ns | 24 B |
| **FancyEnum** `ToStringFancy()` | <1 ns | <1 ns | 0 |
| NetEscapades `ToStringFast()` | <1 ns | <1 ns | 0 |
| Enums.NET `AsString()` | <1 ns | <1 ns | 0 |

For a value that isn't a declared member (`(Medium)999`), the BCL formats the number (13.1 ns, 56 B). FancyEnum returns
an empty string by design (<1 ns, 0 B).

## Parsing a member name

Case-sensitive. "Hit" is a member from the middle of the enum; "miss" is a plausible non-member. Nothing allocates.

| | Medium hit | Medium miss | Large hit | Large miss |
|---|--:|--:|--:|--:|
| BCL `Enum.TryParse` | 45.3 ns | 59.1 ns | 275.4 ns | 488.0 ns |
| **FancyEnum** `TryParseFancy` | 3.9 ns | 2.6 ns | 8.0 ns | 8.5 ns |
| NetEscapades `TryParse` | 37.2 ns | 45.0 ns | 228.3 ns | 413.3 ns |
| Enums.NET `Enums.TryParse` | 11.3 ns | 12.8 ns | 14.3 ns | 13.5 ns |

How: FancyEnum's text parser switches on the input's length, then calls a small generated helper per length that
`switch`es over that length's names as string constants. Roslyn compiles that into a branch on one distinguishing
character plus a single comparison, so a lookup costs about the same whatever the enum's size. Splitting by length
matters for big enums: one method switching over hundreds of names made the JIT zero-initialize hundreds of
temporaries on every call, which cost ~100 ns per lookup on the 200-member enum.

| Case-insensitive (Medium hit) | |
|---|--:|
| BCL | 56.8 ns |
| **FancyEnum** | 8.4 ns |
| NetEscapades | 50.3 ns |
| Enums.NET | 19.0 ns |

| UTF-8 input (Medium hit) | |
|---|--:|
| BCL: transcode into a stack buffer, then `Enum.TryParse` | 27.3 ns |
| **FancyEnum** `TryParseFancy(ReadOnlySpan<byte>)` | 8.4 ns |

## Other operations

BCL vs FancyEnum, plus NetEscapades for `Values`.

| Operation | BCL | FancyEnum | Notes |
|---|--:|--:|---|
| Is a value declared? Contiguous enum | 1.0 ns | <1 ns | FancyEnum: a range check (`!IsUnknown`) |
| Is a value declared? Sparse enum | 3.4 ns | 1.1 ns | |
| Enumerate every member | 33.5 ns, 128 B | 9.6 ns (`Values`) / 8.9 ns (`AsSpan`), 0 B | NetEscapades `GetValues()`: 9.6 ns, 0 B\* |
| `TryFormat` into a buffer | 3.7 ns | 2.2 ns | |
| `HasFlag` | <1 ns | <1 ns | The BCL's is a JIT intrinsic: a tie |
| Format a flag combination | 32.6 ns, 88 B | 16.6 ns, 56 B | |

\* NetEscapades' `GetValues()` returns a new array on every call. .NET 10's JIT stack-allocates it here, because the
array never leaves the benchmark loop; code that stores or passes it on allocates.

## Per-member metadata

BCL rows use the usual hand-rolled reflection (`GetField(...).GetCustomAttribute<T>()`); FancyEnum's are generated
switches.

| Operation | BCL (reflection) | FancyEnum |
|---|--:|--:|
| Format as `[Description]` | 492 ns, 272 B | <1 ns, 0 B |
| Parse from `[Description]` | 6.1 µs, 3,456 B | 3.2 ns, 0 B |
| Read a custom attribute's property | 548 ns, 272 B | <1 ns, 0 B |

## Running them

```bash
cd benchmarks/FancyEnumGenerator.Benchmarks
dotnet run -c Release -- --filter "*" --job short   # ~10 minutes; drop --job short for full precision
```
