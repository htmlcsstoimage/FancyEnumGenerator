namespace FancyEnumGenerator.Tests;

/// <summary>
/// The generated code must compile with no errors and no warnings for every scenario, target, and option set - with
/// XML doc diagnostics on and no implicit usings, as the strictest consumer would build it.
/// </summary>
public class CompilationTests
{
    /// <summary>Repo-wide option sets applied on top of each scenario's own attributes, as MSBuild properties.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> OptionSets = new()
    {
        ["Defaults"] = [],
        ["NoParsing"] = new() { ["FancyEnumGenerateParseMethods"] = "false" },
        ["Everything"] = new() { ["FancyEnumCreateByteParsing"] = "true", ["FancyEnumCreateTryFormat"] = "true", ["FancyEnumCreateStaticReadonlyCollection"] = "true" },
        ["NoInlineArray"] = new() { ["FancyEnumNoInlineArray"] = "true" },
        ["NoInlineArrayCached"] = new() { ["FancyEnumNoInlineArray"] = "true", ["FancyEnumCreateStaticReadonlyCollection"] = "true" },
        ["CaseInsensitive"] = new() { ["FancyEnumParseCaseSensitive"] = "false" },
    };

    public static TheoryData<string, TargetProfile, string> Matrix
    {
        get
        {
            var data = new TheoryData<string, TargetProfile, string>();
            foreach (var scenario in Scenarios.All)
            {
                foreach (var profile in Enum.GetValues<TargetProfile>())
                {
                    if (scenario.Net10Only && profile != TargetProfile.Net10)
                    {
                        continue;
                    }
                    foreach (var optionSet in OptionSets.Keys)
                    {
                        data.Add(scenario.Name, profile, optionSet);
                    }
                }
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void CompilesCleanly(string scenario, TargetProfile profile, string optionSet)
    {
        var run = GeneratorHarness.Run(Scenarios.All.Single(candidate => candidate.Name == scenario).Source, profile, OptionSets[optionSet]);
        run.AssertNoGeneratorDiagnostics();
        run.AssertCompilesCleanly();
    }

    /// <summary>Guards the harness itself: if doc diagnostics silently stopped being reported, every CompilesCleanly case would pass vacuously.</summary>
    [Fact]
    public void HarnessReportsMissingXmlDocs()
    {
        var run = GeneratorHarness.Run("public class Undocumented { }");
        Assert.Contains(run.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken), static diagnostic => diagnostic.Id == "CS1591");
    }

    /// <summary>Also guards the harness: the netstandard2.0 profile must really lack modern APIs, or it proves nothing about downlevel consumers.</summary>
    [Fact]
    public void NetStandardProfileLacksModernApis()
    {
        var run = GeneratorHarness.Run("public static class Probe { public static System.Range R => ..; }", TargetProfile.NetStandard20);
        Assert.Contains(run.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken), static diagnostic => diagnostic.Id == "CS0518");
    }
}
