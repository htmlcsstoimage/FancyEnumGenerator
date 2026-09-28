using System.Text;
using BenchmarkDotNet.Attributes;
using EnumsNET;

namespace FancyEnumGenerator.Benchmarks;

/// <summary>Parsing a member name, case-sensitively, at two enum sizes: a hit on a middle member, and a miss.</summary>
[Config(typeof(BenchmarkConfig))]
public class ParseBenchmarks
{
    private string _medium = "";
    private string _large = "";

    [Params(Position.Middle, Position.Miss)]
    public Position Position { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _medium = Inputs.Name<Medium>(Position);
        _large = Inputs.Name<Large>(Position);
        Inputs.AssertAgree("Medium", (int)Bcl_Medium(), (int)FancyEnum_Medium(), (int)NetEscapades_Medium(), (int)EnumsNet_Medium());
        Inputs.AssertAgree("Large", (int)Bcl_Large(), (int)FancyEnum_Large(), (int)NetEscapades_Large(), (int)EnumsNet_Large());
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Medium")] public Medium Bcl_Medium() => Enum.TryParse<Medium>(_medium, out var result) ? result : default;
    [Benchmark, BenchmarkCategory("Medium")] public Medium FancyEnum_Medium() => Medium.TryParseFancy(_medium, out var result) ? result : default;
    [Benchmark, BenchmarkCategory("Medium")] public NeMedium NetEscapades_Medium() => NeMediumExtensions.TryParse(_medium, out var result) ? result : default;
    [Benchmark, BenchmarkCategory("Medium")] public Medium EnumsNet_Medium() => Enums.TryParse<Medium>(_medium, false, out var result) ? result : default;

    [Benchmark(Baseline = true), BenchmarkCategory("Large")] public Large Bcl_Large() => Enum.TryParse<Large>(_large, out var result) ? result : default;
    [Benchmark, BenchmarkCategory("Large")] public Large FancyEnum_Large() => Large.TryParseFancy(_large, out var result) ? result : default;
    [Benchmark, BenchmarkCategory("Large")] public NeLarge NetEscapades_Large() => NeLargeExtensions.TryParse(_large, out var result) ? result : default;
    [Benchmark, BenchmarkCategory("Large")] public Large EnumsNet_Large() => Enums.TryParse<Large>(_large, false, out var result) ? result : default;
}

/// <summary>Parsing with case ignored (input upper-cased) on the medium enum.</summary>
[Config(typeof(BenchmarkConfig))]
public class ParseIgnoreCaseBenchmarks
{
    private readonly string _input = Inputs.Name<Medium>(Position.Middle).ToUpperInvariant();

    [GlobalSetup]
    public void Setup() => Inputs.AssertAgree("IgnoreCase", (int)Bcl(), (int)FancyEnum(), (int)NetEscapades(), (int)EnumsNet());

    [Benchmark(Baseline = true)] public Medium Bcl() => Enum.TryParse<Medium>(_input, ignoreCase: true, out var result) ? result : default;
    [Benchmark] public Medium FancyEnum() => Medium.TryParseFancy(_input, ignoreCase: true, out var result) ? result : default;
    [Benchmark] public NeMedium NetEscapades() => NeMediumExtensions.TryParse(_input, out var result, ignoreCase: true) ? result : default;
    [Benchmark] public Medium EnumsNet() => Enums.TryParse<Medium>(_input, true, out var result) ? result : default;
}

/// <summary>
/// Parsing UTF-8 input (as read off a socket or out of JSON). FancyEnum parses the bytes directly; the BCL has no
/// byte overload, so its row transcodes into a stack buffer first - the cheapest route it allows.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ParseUtf8Benchmarks
{
    private readonly byte[] _input = Encoding.UTF8.GetBytes(Inputs.Name<Medium>(Position.Middle));

    [GlobalSetup]
    public void Setup() => Inputs.AssertAgree("Utf8", Bcl(), FancyEnum());

    [Benchmark(Baseline = true)]
    public Medium Bcl()
    {
        Span<char> buffer = stackalloc char[64];
        var length = Encoding.UTF8.GetChars(_input, buffer);
        return Enum.TryParse<Medium>(buffer[..length], out var result) ? result : default;
    }

    [Benchmark] public Medium FancyEnum() => Medium.TryParseFancy(_input, out var result) ? result : default;
}
