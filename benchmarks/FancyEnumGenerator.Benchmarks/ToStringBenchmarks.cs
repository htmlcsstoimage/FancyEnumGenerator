using BenchmarkDotNet.Attributes;
using EnumsNET;

namespace FancyEnumGenerator.Benchmarks;

/// <summary>Formatting a value as its name: a declared member at two enum sizes, and a value that isn't declared.</summary>
[Config(typeof(BenchmarkConfig))]
public class ToStringBenchmarks
{
    private readonly Medium _medium = (Medium)12;
    private readonly NeMedium _neMedium = (NeMedium)12;
    private readonly Large _large = (Large)100;
    private readonly NeLarge _neLarge = (NeLarge)100;
    private readonly Medium _undeclared = (Medium)999;

    [GlobalSetup]
    public void Setup()
    {
        Inputs.AssertAgree("Medium", Bcl_Medium(), FancyEnum_Medium(), NetEscapades_Medium(), EnumsNet_Medium());
        Inputs.AssertAgree("Large", Bcl_Large(), FancyEnum_Large(), NetEscapades_Large(), EnumsNet_Large());
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Medium")] public string Bcl_Medium() => _medium.ToString();
    [Benchmark, BenchmarkCategory("Medium")] public string FancyEnum_Medium() => _medium.ToStringFancy();
    [Benchmark, BenchmarkCategory("Medium")] public string NetEscapades_Medium() => _neMedium.ToStringFast();
    [Benchmark, BenchmarkCategory("Medium")] public string EnumsNet_Medium() => _medium.AsString();

    [Benchmark(Baseline = true), BenchmarkCategory("Large")] public string Bcl_Large() => _large.ToString();
    [Benchmark, BenchmarkCategory("Large")] public string FancyEnum_Large() => _large.ToStringFancy();
    [Benchmark, BenchmarkCategory("Large")] public string NetEscapades_Large() => _neLarge.ToStringFast();
    [Benchmark, BenchmarkCategory("Large")] public string EnumsNet_Large() => _large.AsString();

    // Semantics differ here by design: the BCL formats the number ("999", allocating); FancyEnum returns an empty
    // string for anything that isn't a declared member.
    [Benchmark(Baseline = true), BenchmarkCategory("Undeclared")] public string Bcl_Undeclared() => _undeclared.ToString();
    [Benchmark, BenchmarkCategory("Undeclared")] public string FancyEnum_Undeclared() => _undeclared.ToStringFancy();
}
