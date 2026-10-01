using BenchmarkDotNet.Attributes;

namespace FancyEnumGenerator.Benchmarks;

/// <summary>Checking whether a numeric value is a declared member, for a contiguous and a sparse enum.</summary>
[Config(typeof(BenchmarkConfig))]
public class IsDefinedBenchmarks
{
    // Never 0: FancyEnum treats Unknown as not-a-real-member (IsUnknown), where the BCL counts it as defined.
    private readonly Medium _medium = (Medium)12;
    private readonly Sparse _sparse = (Sparse)(3 + 11 * 7);

    [GlobalSetup]
    public void Setup()
    {
        Inputs.AssertAgree("Contiguous", Bcl_Contiguous(), FancyEnum_Contiguous());
        Inputs.AssertAgree("Sparse", Bcl_Sparse(), FancyEnum_Sparse());
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Contiguous")] public bool Bcl_Contiguous() => Enum.IsDefined(_medium);
    [Benchmark, BenchmarkCategory("Contiguous")] public bool FancyEnum_Contiguous() => !_medium.IsUnknown;

    [Benchmark(Baseline = true), BenchmarkCategory("Sparse")] public bool Bcl_Sparse() => Enum.IsDefined(_sparse);
    [Benchmark, BenchmarkCategory("Sparse")] public bool FancyEnum_Sparse() => !_sparse.IsUnknown;
}

/// <summary>
/// Enumerating every member (summing the values, so the loop can't be elided), once per FancyEnum ValuesType. The BCL
/// and NetEscapades both return a new array per call; on .NET 10 the JIT can stack-allocate NetEscapades' array here,
/// because once inlined it never escapes this loop, but code that stores or passes the array on still pays for it.
/// FancyEnum's Values/AsSpan exclude the Unknown member, so it walks 24 members where the others walk 25.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ValuesBenchmarks
{
    [Benchmark(Baseline = true)]
    public int Bcl()
    {
        var sum = 0;
        foreach (var value in Enum.GetValues<Medium>())
        {
            sum += (int)value;
        }
        return sum;
    }

    /// <summary>The default: a span over static data in the assembly.</summary>
    [Benchmark]
    public int FancyEnum_Span()
    {
        var sum = 0;
        foreach (var value in Medium.Values)
        {
            sum += (int)value;
        }
        return sum;
    }

    /// <summary>A fresh inline-array copy per call, which can be stored or kept across an await.</summary>
    [Benchmark]
    public int FancyEnum_InlineArray()
    {
        var sum = 0;
        foreach (var value in MediumInline.Values)
        {
            sum += (int)value;
        }
        return sum;
    }

    /// <summary>The cached IReadOnlyList, enumerated through the interface.</summary>
    [Benchmark]
    public int FancyEnum_StaticCollection()
    {
        var sum = 0;
        foreach (var value in MediumCached.Values)
        {
            sum += (int)value;
        }
        return sum;
    }

    /// <summary>The span over the cached array that StaticCollection also generates.</summary>
    [Benchmark]
    public int FancyEnum_StaticCollection_AsSpan()
    {
        var sum = 0;
        foreach (var value in MediumCached.AsSpan)
        {
            sum += (int)value;
        }
        return sum;
    }

    [Benchmark]
    public int NetEscapades()
    {
        var sum = 0;
        foreach (var value in NeMediumExtensions.GetValues())
        {
            sum += (int)value;
        }
        return sum;
    }
}

/// <summary>Formatting into a caller-supplied buffer, with no string allocated.</summary>
[Config(typeof(BenchmarkConfig))]
public class TryFormatBenchmarks
{
    private readonly Medium _medium = (Medium)12;
    private readonly char[] _buffer = new char[64];

    [Benchmark(Baseline = true)] public int Bcl() => Enum.TryFormat(_medium, _buffer, out var written) ? written : -1;
    [Benchmark] public int FancyEnum() => _medium.TryFormat(_buffer, out var written) ? written : -1;
}
