using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Smoke;

// Everything here sticks to APIs that exist on .NET Framework 4.8 too: no Enum.GetValues<T>, no ranges, no string.Create.
var failures = new List<string>();
void Check(bool condition, string what)
{
    if (!condition)
    {
        failures.Add(what);
    }
}

// Formatting and parsing.
Check(Fruit.Apple.ToStringFancy() == "Apple", "ToStringFancy");
Check(Fruit.TryParseFancy("Banana", out var banana) && banana == Fruit.Banana, "TryParseFancy");
Check(!Fruit.TryParseFancy("banana", out _), "case-sensitive by default");
Check(Fruit.ParseOrUnknown("nope") == Fruit.Unknown, "ParseOrUnknown");
Check(((Fruit)99).IsUnknown && Fruit.FromUnderlying(99) == Fruit.Unknown, "undeclared values");
Check(FruitFancyEnumExtensions.FirstNonUnknown == Fruit.Apple, "FirstNonUnknown const");

// Flags: a combination that isn't a declared member (downlevel: the stack-buffer path, not string.Create).
var readExecute = Permission.Read | Permission.Execute;
Check(readExecute.ToStringFancy() == "Read|Execute", "flags combination");
Check(readExecute.HasFlagFancy(Permission.Execute), "HasFlagFancy");
var flagBuffer = new char[Permission.LongestCharLength];
Check(readExecute.TryFormat(flagBuffer, out var flagLength) && new string(flagBuffer, 0, flagLength) == "Read|Execute", "flags TryFormat");

// Parsing strategies: per-length helpers (text), upper-casing (ignore case), packed ASCII (UTF-8).
foreach (var name in new[] { "Alpha", "Sigma", "WestCoastRegion03", "WestCoastRegion05" })
{
    Check(Region.TryParseFancy(name, out var exact) && exact.ToStringFancy() == name, $"text parse {name}");
    Check(Region.TryParseFancy(name.ToLowerInvariant(), out var lower) && lower == exact, $"ignore-case parse {name}");
    Check(Region.TryParseFancy(Encoding.UTF8.GetBytes(name.ToUpperInvariant()), out var bytes) && bytes == exact, $"UTF-8 parse {name}");
}
Check(!Region.TryParseFancy("WestCoastRegion06", out _), "near miss (text)");
Check(!Region.TryParseFancy(Encoding.UTF8.GetBytes("WestCoastRegion06"), out _), "near miss (UTF-8)");
Check(Region.TryParseFancy("Alpha", ignoreCase: false, out _) && !Region.TryParseFancy("ALPHA", ignoreCase: false, out _), "explicit case-sensitive overload");

// Field mappings, UTF-8 values, per-field parsing and formatting.
Check(Region.Delta.Code == "D3", "field mapping");
Check(Encoding.UTF8.GetString(Region.Delta.CodeBytes.ToArray()) == "D3", "UTF-8 field value");
Check(Region.TryParseFrom_Code("G4", out var gamma) && gamma == Region.Gamma, "TryParseFrom_Code");
Check(Region.TryParseFrom_Code("g4", out _) && !Region.TryParseFrom_Code("g4", ignoreCase: false, out _), "TryParseFrom_Code inherits the enum's case-insensitivity");
var codeBuffer = new char[Region.Code_LongestCharLength];
Check(Region.Omega.TryFormat_Code(codeBuffer, out var codeLength) && new string(codeBuffer, 0, codeLength) == "O5", "TryFormat_Code");

// Cached collection and its span. Values is an inline-array struct on .NET 8+ and an IReadOnlyList (over a plain
// array) downlevel; both have an indexer, and AsSpan is the same everywhere.
Check(Region.Values[0] == Region.Alpha, "Values");
Check(Region.AsSpan.Length == 11 && Region.AsSpan[10] == Region.WestCoastRegion05, "AsSpan");

// Prefix checks.
Check(Region.IsValidPrefixFancy(Encoding.UTF8.GetBytes("WestCoast"), Encoding.UTF8.GetBytes("Region0")), "IsValidPrefixFancy hit");
Check(!Region.IsValidPrefixFancy(Encoding.UTF8.GetBytes("WestCoast"), Encoding.UTF8.GetBytes("X")), "IsValidPrefixFancy miss");

// Member sets.
Check(Planet.Mars.Label == "red" && Planet.Mars.Order == 40, "member set values");
Check(Planet.Venus.Order == -1, "member set DefaultValue");

var runtime = RuntimeInformation.FrameworkDescription;
if (failures.Count > 0)
{
    Console.WriteLine($"{failures.Count} check(s) failed on {runtime}:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"  {failure}");
    }
    return 1;
}
Console.WriteLine($"All checks passed on {runtime}.");
return 0;
