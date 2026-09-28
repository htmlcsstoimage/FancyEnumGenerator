using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Basic.Reference.Assemblies;
using FancyEnumGenerator.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FancyEnumGenerator.Tests;

/// <summary>The consumer target a test compiles for: picks the reference assemblies and the preprocessor symbols.</summary>
public enum TargetProfile
{
    Net10,
    NetStandard20
}

/// <summary>One generator pass over a test compilation, plus the compilation with the generated sources added.</summary>
internal sealed record GeneratorRun
{
    public required GeneratorDriver Driver { get; init; }
    public required Compilation InputCompilation { get; init; }
    public required Compilation OutputCompilation { get; init; }
    public required CSharpParseOptions ParseOptions { get; init; }

    public GeneratorDriverRunResult Result => Driver.GetRunResult();

    /// <summary>Diagnostics the generator itself reported (the HENUM ids).</summary>
    public ImmutableArray<Diagnostic> GeneratorDiagnostics => Result.Diagnostics;

    /// <summary>The generated file whose hint name ends with <paramref name="hintNameSuffix"/>.</summary>
    public string GeneratedSource(string hintNameSuffix) =>
        Result.Results.Single().GeneratedSources.Single(source => source.HintName.EndsWith(hintNameSuffix, StringComparison.Ordinal)).SourceText.ToString();

    public IEnumerable<string> HintNames => Result.Results.Single().GeneratedSources.Select(static source => source.HintName);

    /// <summary>
    /// Fails unless the generated code compiles cleanly: no errors anywhere, and no warnings at all inside generated
    /// files. That includes CS1591 (missing XML docs), because every harness compilation parses with
    /// <see cref="DocumentationMode.Diagnose"/>, as a consumer with GenerateDocumentationFile would. Warnings in the
    /// test's own source are ignored, so test sources don't need doc comments.
    /// </summary>
    public void AssertCompilesCleanly()
    {
        var generatedTrees = Result.GeneratedTrees.ToHashSet();
        var problems = OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error ||
                                 diagnostic.Severity == DiagnosticSeverity.Warning && diagnostic.Location.SourceTree is { } tree && generatedTrees.Contains(tree))
            .ToArray();
        Assert.True(problems.Length == 0, "Generated code did not compile cleanly:\n" + string.Join("\n", problems.Select(Describe)));
    }

    public void AssertNoGeneratorDiagnostics() =>
        Assert.True(GeneratorDiagnostics.IsEmpty, "Unexpected generator diagnostics:\n" + string.Join("\n", GeneratorDiagnostics.Select(Describe)));

    /// <summary>Re-runs the same driver (keeping its incremental cache) over a changed compilation.</summary>
    public GeneratorRun Rerun(Func<Compilation, Compilation> change)
    {
        var input = change(InputCompilation);
        var driver = Driver.RunGeneratorsAndUpdateCompilation(input, out var output, out _);
        return this with { Driver = driver, InputCompilation = input, OutputCompilation = output };
    }

    private static string Describe(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        return $"  {diagnostic.Id} {Path.GetFileName(span.Path)}({span.StartLinePosition.Line + 1}): {diagnostic.GetMessage()}";
    }
}

internal static class GeneratorHarness
{
    private static readonly string[] Net10Symbols =
    [
        "NET", "NET10_0", "NETCOREAPP",
        "NET10_0_OR_GREATER", "NET9_0_OR_GREATER", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER", "NET6_0_OR_GREATER", "NET5_0_OR_GREATER",
        "NETCOREAPP3_1_OR_GREATER", "NETCOREAPP3_0_OR_GREATER", "NETCOREAPP2_2_OR_GREATER", "NETCOREAPP2_1_OR_GREATER",
        "NETCOREAPP2_0_OR_GREATER", "NETCOREAPP1_1_OR_GREATER", "NETCOREAPP1_0_OR_GREATER"
    ];

    private static readonly string[] NetStandard20Symbols =
    [
        "NETSTANDARD", "NETSTANDARD2_0",
        "NETSTANDARD2_0_OR_GREATER", "NETSTANDARD1_6_OR_GREATER", "NETSTANDARD1_5_OR_GREATER", "NETSTANDARD1_4_OR_GREATER",
        "NETSTANDARD1_3_OR_GREATER", "NETSTANDARD1_2_OR_GREATER", "NETSTANDARD1_1_OR_GREATER", "NETSTANDARD1_0_OR_GREATER"
    ];

    private static readonly MetadataReference AttributesReference = MetadataReference.CreateFromFile(typeof(FancyEnumAttribute).Assembly.Location);

    private static readonly ImmutableArray<MetadataReference> Net10References =
        [.. Net100.References.All, AttributesReference];

    private static readonly ImmutableArray<MetadataReference> NetStandard20References =
    [
        .. NetStandard20.References.All,
        MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "refs", "netstandard2.0", "System.Memory.dll")),
        AttributesReference
    ];

    /// <summary>
    /// Runs the generator over <paramref name="sources"/>. <paramref name="msbuildProperties"/> are MSBuild property
    /// names as a consumer would write them (e.g. <c>FancyEnumAllowNoUnknown</c>), surfaced the same way the
    /// package's buildTransitive targets do: as <c>build_property.*</c> global analyzer options.
    /// </summary>
    public static GeneratorRun Run(IEnumerable<string> sources, TargetProfile profile = TargetProfile.Net10, IReadOnlyDictionary<string, string>? msbuildProperties = null)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithDocumentationMode(DocumentationMode.Diagnose)
            .WithPreprocessorSymbols(profile == TargetProfile.Net10 ? Net10Symbols : NetStandard20Symbols);
        var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, parseOptions, path: $"Source{index}.cs"));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            profile == TargetProfile.Net10 ? Net10References : NetStandard20References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var globalOptions = (msbuildProperties ?? ImmutableDictionary<string, string>.Empty)
            .ToImmutableDictionary(static pair => $"build_property.{pair.Key}", static pair => pair.Value);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new FancyEnumSourceGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            optionsProvider: new TestOptionsProvider(globalOptions),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return new GeneratorRun { Driver = driver, InputCompilation = compilation, OutputCompilation = output, ParseOptions = parseOptions };
    }

    public static GeneratorRun Run(string source, TargetProfile profile = TargetProfile.Net10, IReadOnlyDictionary<string, string>? msbuildProperties = null) =>
        Run([source], profile, msbuildProperties);

    /// <summary>Runs the package's analyzers (not the generator) over <paramref name="source"/> after generation.</summary>
    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzersAsync(string source)
    {
        var run = Run(source);
        return await run.OutputCompilation
            .WithAnalyzers([new MemberSetWithoutFancyEnumAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestOptionsProvider(ImmutableDictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(globalOptions);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestOptions.Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestOptions.Empty;
    }

    private sealed class TestOptions(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public static readonly TestOptions Empty = new(ImmutableDictionary<string, string>.Empty);
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }
}
