namespace FancyEnumGenerator.Tests;

/// <summary>
/// Option precedence: explicit per-enum attribute, then <c>[assembly: FancyEnumDefaults]</c>, then MSBuild
/// <c>FancyEnum*</c> properties, then the library default.
/// </summary>
public class OptionTests
{
    private const string Usings = "using FancyEnumGenerator.Attributes;\n";
    private const string NoUnknownEnum = "[FancyEnum] public enum E { A = 0, B = 1 }";

    private static Dictionary<string, string> MsBuild(string name, string value) => new() { [$"FancyEnum{name}"] = value };

    private static bool ReportsMissingUnknown(GeneratorRun run) => run.GeneratorDiagnostics.Any(static diagnostic => diagnostic.Id == "HENUM001");

    [Fact]
    public void LibraryDefaultApplies() =>
        Assert.True(ReportsMissingUnknown(GeneratorHarness.Run(Usings + NoUnknownEnum)));

    [Fact]
    public void MsBuildOverridesLibraryDefault() =>
        Assert.False(ReportsMissingUnknown(GeneratorHarness.Run(Usings + NoUnknownEnum, msbuildProperties: MsBuild("AllowNoUnknown", "true"))));

    [Fact]
    public void MsBuildValuesAreCaseInsensitive() =>
        Assert.False(ReportsMissingUnknown(GeneratorHarness.Run(Usings + NoUnknownEnum, msbuildProperties: MsBuild("AllowNoUnknown", "True"))));

    [Fact]
    public void AssemblyDefaultOverridesMsBuild() =>
        Assert.True(ReportsMissingUnknown(GeneratorHarness.Run(
            Usings + "[assembly: FancyEnumDefaults(AllowNoUnknown = false)]\n" + NoUnknownEnum,
            msbuildProperties: MsBuild("AllowNoUnknown", "true"))));

    [Fact]
    public void ExplicitEnumSettingOverridesAssemblyDefault() =>
        Assert.False(ReportsMissingUnknown(GeneratorHarness.Run(
            Usings + "[assembly: FancyEnumDefaults(AllowNoUnknown = false)]\n[FancyEnum(AllowNoUnknown = true)] public enum E { A = 0, B = 1 }")));

    [Fact]
    public void UnsetAssemblyPropertiesFallThroughToMsBuild() =>
        // The assembly attribute is present but doesn't mention AllowNoUnknown, so MSBuild still decides it.
        Assert.False(ReportsMissingUnknown(GeneratorHarness.Run(
            Usings + "[assembly: FancyEnumDefaults(CreateTryFormat = true)]\n" + NoUnknownEnum,
            msbuildProperties: MsBuild("AllowNoUnknown", "true"))));

    [Fact]
    public void EnumDefaultToStringBehaviorFromMsBuild()
    {
        var run = GeneratorHarness.Run(Usings + "[FancyEnum] public enum E { Unknown, FirstOne }", msbuildProperties: MsBuild("DefaultToStringBehavior", "NameOfLower"));
        Assert.Contains("\"firstone\"", run.GeneratedSource("E.FancyEnum.g.cs"));
    }

    [Fact]
    public void DefaultToStringCustomFieldFromMsBuild()
    {
        var run = GeneratorHarness.Run(
            Usings + """[FancyEnum] public enum E { Unknown, [FancyEnumMember("Code", "first-code")] First }""",
            msbuildProperties: new Dictionary<string, string>
            {
                ["FancyEnumDefaultToStringBehavior"] = "CustomFieldFallback",
                ["FancyEnumDefaultToStringCustomField"] = "Code"
            });
        run.AssertNoGeneratorDiagnostics();
        Assert.Contains("thisEnum.First => \"first-code\"", run.GeneratedSource("E.FancyEnum.g.cs"));
    }

    [Fact]
    public void GenerateParseMethodsFalseDropsAllParsing()
    {
        var run = GeneratorHarness.Run(
            Usings + """[FancyEnum(CreateByteParsing = true), FancyEnumMemberMappingSettings("F", ParseFrom = true)] public enum E { Unknown, [FancyEnumMember("F", "a")] A }""",
            msbuildProperties: MsBuild("GenerateParseMethods", "false"));
        var source = run.GeneratedSource("E.FancyEnum.g.cs");
        Assert.DoesNotContain("TryParseFancy", source);
        Assert.DoesNotContain("ParseOrUnknown", source);
        Assert.DoesNotContain("IsValidPrefixFancy", source);
        Assert.DoesNotContain("TryParseFrom_", source);
        run.AssertCompilesCleanly();
    }

    [Fact]
    public void FieldParsersInheritAssemblyWideCaseSensitivity()
    {
        var run = GeneratorHarness.Run(Usings + """
            [assembly: FancyEnumDefaults(ParseCaseSensitive = false)]
            [FancyEnum, FancyEnumMemberMappingSettings("F", ParseFrom = true), FancyEnumMemberMappingSettings("G", ParseFrom = true, ParseCaseSensitive = true)]
            public enum E { Unknown, [FancyEnumMember("F", "f1"), FancyEnumMember("G", "g1")] A, [FancyEnumMember("F", "f2"), FancyEnumMember("G", "g2")] B }
            """);
        var source = run.GeneratedSource("E.FancyEnum.g.cs");
        Assert.Contains("thisEnum.TryParseFrom_F(input, true, out result)", source); // default overload ignores case
        Assert.Contains("thisEnum.TryParseFrom_G(input, false, out result)", source);
    }

    [Fact]
    public void IsValidPrefixIsOptInAndIndependentOfByteParsing()
    {
        var byteParsingOnly = GeneratorHarness.Run(Usings + "[FancyEnum(CreateByteParsing = true)] public enum E { Unknown, A, B }");
        Assert.DoesNotContain("IsValidPrefixFancy", byteParsingOnly.GeneratedSource("E.FancyEnum.g.cs"));

        var prefixOnly = GeneratorHarness.Run(Usings + "[FancyEnum(CreateIsValidPrefix = true)] public enum E { Unknown, A, B }");
        var source = prefixOnly.GeneratedSource("E.FancyEnum.g.cs");
        Assert.Contains("IsValidPrefixFancy", source);
        Assert.DoesNotContain("TryParseFancy(ByteSpan", source);
        prefixOnly.AssertCompilesCleanly();
    }

    [Fact]
    public void IsValidPrefixFromMsBuild()
    {
        var run = GeneratorHarness.Run(Usings + "[FancyEnum] public enum E { Unknown, A, B }", msbuildProperties: MsBuild("CreateIsValidPrefix", "true"));
        Assert.Contains("IsValidPrefixFancy", run.GeneratedSource("E.FancyEnum.g.cs"));
    }

    [Theory]
    [InlineData(null, "E.FancyEnum.g.cs")]
    [InlineData("true", "E.FancyEnum.g.cs")]
    [InlineData("false", "E.FancyEnum.cs")]
    public void GeneratedFileSuffix(string? useGeneratedFileSuffix, string expectedHintName)
    {
        var run = GeneratorHarness.Run(
            Usings + "[FancyEnum] public enum E { Unknown, A }",
            msbuildProperties: useGeneratedFileSuffix is null ? null : MsBuild("UseGeneratedFileSuffix", useGeneratedFileSuffix));
        Assert.Contains(expectedHintName, run.HintNames);
    }
}
