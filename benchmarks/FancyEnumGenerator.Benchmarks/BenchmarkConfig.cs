using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Order;

namespace FancyEnumGenerator.Benchmarks;

/// <summary>
/// Shared by every benchmark class: allocations matter as much as time here, and each category (an enum size or a
/// variant of the operation) is its own table, with the BCL as the baseline row.
/// </summary>
public sealed class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(CategoriesColumn.Default);
        AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams);
        HideColumns(Column.Median, Column.RatioSD, Column.Gen0);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Declared));
    }
}

/// <summary>Which input a parse benchmark uses.</summary>
public enum Position
{
    /// <summary>A member from the middle of the enum.</summary>
    Middle,
    /// <summary>Not a member at all: the worst case for anything that searches.</summary>
    Miss
}

internal static class Inputs
{
    /// <summary>The name of the middle member (Unknown excluded), or a plausible non-member.</summary>
    public static string Name<TEnum>(Position position) where TEnum : struct, Enum
    {
        var names = Enum.GetNames<TEnum>().Where(static name => name != "Unknown").ToArray();
        return position == Position.Middle ? names[names.Length / 2] : "NotAMember" + names[0][..3];
    }

    /// <summary>Throws unless every library produced the same answer, so a benchmark can never quietly compare different work.</summary>
    public static void AssertAgree<T>(string what, params T[] results)
    {
        if (results.Distinct().Count() != 1)
        {
            throw new InvalidOperationException($"{what}: libraries disagree: {string.Join(", ", results)}");
        }
    }
}
