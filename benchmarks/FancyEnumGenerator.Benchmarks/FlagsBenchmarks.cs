using BenchmarkDotNet.Attributes;

namespace FancyEnumGenerator.Benchmarks;

/// <summary>Testing a flag, and formatting a combination of flags that isn't itself a declared member.</summary>
[Config(typeof(BenchmarkConfig))]
public class FlagsBenchmarks
{
    private readonly Permissions _value = Permissions.Read | Permissions.Write | Permissions.Audit;

    [GlobalSetup]
    public void Setup() => Inputs.AssertAgree("HasFlag", Bcl_HasFlag(), FancyEnum_HasFlag());

    [Benchmark(Baseline = true), BenchmarkCategory("HasFlag")] public bool Bcl_HasFlag() => _value.HasFlag(Permissions.Write);
    [Benchmark, BenchmarkCategory("HasFlag")] public bool FancyEnum_HasFlag() => _value.HasFlagFancy(Permissions.Write);

    // Output differs cosmetically: the BCL writes "Read, Write, Audit", FancyEnum "Read|Write|Audit".
    [Benchmark(Baseline = true), BenchmarkCategory("FormatCombination")] public string Bcl_Format() => _value.ToString();
    [Benchmark, BenchmarkCategory("FormatCombination")] public string FancyEnum_Format() => _value.ToStringFancy();
}
