using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FancyEnumGenerator.Tests;

/// <summary>
/// The generator runs on every keystroke in the IDE, so an edit that doesn't affect an enum must not re-emit its
/// source. That only holds if every model flowing through the pipeline is value-equatable; one stray symbol, syntax
/// node or Location in a model would silently make every run a cache miss, which these tests catch.
/// </summary>
public class IncrementalTests
{
    private const string EnumSource = """
        using FancyEnumGenerator.Attributes;

        namespace Demo;

        [FancyEnum, FancyEnumMemberMappingSettings("Label", ParseFrom = true)]
        public enum Fruit { Unknown, [FancyEnumMember("Label", "apple")] Apple, Banana }
        """;

    private static IncrementalStepRunReason[] OutputReasons(GeneratorRun run) =>
        run.Result.Results.Single().TrackedOutputSteps
            .SelectMany(static step => step.Value)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();

    private static Compilation AddTree(Compilation compilation, string source, CSharpParseOptions parseOptions) =>
        compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(source, parseOptions, path: $"Added{compilation.SyntaxTrees.Count()}.cs"));

    private static Compilation ReplaceFirstTree(Compilation compilation, string newSource, CSharpParseOptions parseOptions)
    {
        var original = compilation.SyntaxTrees.First();
        return compilation.ReplaceSyntaxTree(original, CSharpSyntaxTree.ParseText(newSource, parseOptions, path: original.FilePath));
    }

    [Fact]
    public void UnrelatedEditIsFullyCached()
    {
        var first = GeneratorHarness.Run(EnumSource);
        var second = first.Rerun(compilation => AddTree(compilation, "namespace Demo; public class Unrelated { }", first.ParseOptions));
        Assert.All(OutputReasons(second), static reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, $"output was {reason}"));
    }

    [Fact]
    public void TriviaOnlyEditToTheEnumIsFullyCached()
    {
        var first = GeneratorHarness.Run(EnumSource);
        var second = first.Rerun(compilation => ReplaceFirstTree(compilation, EnumSource.Replace("namespace Demo;", "// a comment\nnamespace Demo;"), first.ParseOptions));
        Assert.All(OutputReasons(second), static reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, $"output was {reason}"));
    }

    [Fact]
    public void RealEditToTheEnumRegenerates()
    {
        var first = GeneratorHarness.Run(EnumSource);
        var second = first.Rerun(compilation => ReplaceFirstTree(compilation, EnumSource.Replace("Banana }", "Banana, Cherry }"), first.ParseOptions));
        Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(second));
        Assert.Contains("Cherry", second.GeneratedSource("Fruit.FancyEnum.g.cs"));
    }

    private const string MemberSetSource = """
        using FancyEnumGenerator.Attributes;

        namespace Demo;

        [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
        public sealed class MetaAttribute : System.Attribute { public string? Label { get; set; } }

        [FancyEnum]
        public enum Vegetable { Unknown, [Meta(Label = "carrot")] Carrot }
        """;

    [Fact]
    public void MemberSetEnumIsCached()
    {
        var first = GeneratorHarness.Run(MemberSetSource);
        var second = first.Rerun(compilation => AddTree(compilation, "namespace Demo; public class Unrelated { }", first.ParseOptions));
        Assert.All(OutputReasons(second), static reason => Assert.True(reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, $"output was {reason}"));
    }

    [Fact]
    public void EditingAMemberSetClassRegeneratesItsEnums()
    {
        var first = GeneratorHarness.Run(MemberSetSource);
        var second = first.Rerun(compilation => ReplaceFirstTree(compilation, MemberSetSource.Replace("public string? Label { get; set; }", "public string? Label { get; set; } public int Size { get; set; }"), first.ParseOptions));
        Assert.Contains(IncrementalStepRunReason.Modified, OutputReasons(second));
        Assert.Contains("Size", second.GeneratedSource("Vegetable.FancyEnum.g.cs"));
    }
}
