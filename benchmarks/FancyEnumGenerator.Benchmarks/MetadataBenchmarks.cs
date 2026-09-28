using System.ComponentModel;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace FancyEnumGenerator.Benchmarks;

/// <summary>
/// Per-member metadata, BCL vs FancyEnum: formatting as, and parsing from, a <see cref="DescriptionAttribute"/>, and
/// reading a custom attribute's property. The BCL rows are the usual hand-rolled reflection.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class MetadataBenchmarks
{
    private readonly Medium _medium = (Medium)12;
    private readonly MediumDescribed _described = (MediumDescribed)12;
    private readonly Station _station = (Station)12;
    private string _description = "";

    [GlobalSetup]
    public void Setup()
    {
        _description = Bcl_ToDescription();
        Inputs.AssertAgree("ToDescription", Bcl_ToDescription(), FancyEnum_ToDescription());
        Inputs.AssertAgree("ParseDescription", (int)Bcl_ParseDescription(), (int)FancyEnum_ParseDescription());
        Inputs.AssertAgree("CustomAttribute", Bcl_CustomAttribute(), FancyEnum_CustomAttribute());
    }

    [Benchmark(Baseline = true), BenchmarkCategory("ToDescription")]
    public string Bcl_ToDescription() => typeof(Medium).GetField(_medium.ToString())!.GetCustomAttribute<DescriptionAttribute>()!.Description;

    [Benchmark, BenchmarkCategory("ToDescription")] public string FancyEnum_ToDescription() => _described.ToStringFancy();

    [Benchmark(Baseline = true), BenchmarkCategory("ParseDescription")]
    public Medium Bcl_ParseDescription()
    {
        foreach (var field in typeof(Medium).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetCustomAttribute<DescriptionAttribute>()?.Description == _description)
            {
                return (Medium)field.GetValue(null)!;
            }
        }
        return default;
    }

    [Benchmark, BenchmarkCategory("ParseDescription")] public MediumDescribed FancyEnum_ParseDescription() => MediumDescribed.TryParseFancy(_description, out var result) ? result : default;

    [Benchmark(Baseline = true), BenchmarkCategory("CustomAttribute")]
    public string? Bcl_CustomAttribute() => typeof(Station).GetField(_station.ToString())!.GetCustomAttribute<StationInfoAttribute>()!.Label;

    [Benchmark, BenchmarkCategory("CustomAttribute")] public string? FancyEnum_CustomAttribute() => _station.Label;
}
