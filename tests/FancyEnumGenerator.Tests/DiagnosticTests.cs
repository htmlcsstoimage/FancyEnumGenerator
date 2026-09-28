namespace FancyEnumGenerator.Tests;

/// <summary>Each HENUM diagnostic fires for the misuse it describes, and stays quiet for the look-alike valid cases.</summary>
public class DiagnosticTests
{
    private const string Usings = "using FancyEnumGenerator.Attributes;\n";

    public static TheoryData<string, string, string> Triggers => new()
    {
        { "HENUM001", "missing Unknown", "[FancyEnum] public enum E { A = 0, B = 1 }" },
        { "HENUM001", "Unknown not zero", "[FancyEnum] public enum E { A = 0, Unknown = 1 }" },
        { "HENUM002", "gap between values", "[FancyEnum] public enum E { Unknown = 0, A = 1, B = 5 }" },
        { "HENUM002", "flags enum explicitly opted back in to the check", "[System.Flags, FancyEnum(AllowNonContiguous = false)] public enum E { Unknown = 0, A = 1, B = 2, C = 4 }" },
        { "HENUM003", "member maps a field twice", """[FancyEnum] public enum E { Unknown, [FancyEnumMember("F", "a"), FancyEnumMember("F", "b")] A }""" },
        { "HENUM003", "mixed field types", """[FancyEnum] public enum E { Unknown, [FancyEnumMember("F", "a")] A, [FancyEnumMember<int>("F", 1)] B }""" },
        { "HENUM003", "settings declared twice", """[FancyEnum, FancyEnumMemberMappingSettings("F"), FancyEnumMemberMappingSettings("F")] public enum E { Unknown, [FancyEnumMember("F", "a")] A }""" },
        { "HENUM003", "NotDefined name fallback on non-string field", """[FancyEnum] [FancyEnumMemberMappingSettings("F", NotDefined = FancyEnumMemberFallbackOption.NameOf)] public enum E { Unknown, [FancyEnumMember<int>("F", 1)] A, B }""" },
        { "HENUM003", "member set and explicit mapping share a field", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class CssAttribute : System.Attribute { public string? Css { get; set; } }
            [FancyEnum] public enum E { Unknown, [Css(Css = "a"), FancyEnumMember("Css", "b")] A }
            """ },
        { "HENUM004", "duplicate numeric value", "[FancyEnum] public enum E { Unknown = 0, A = 1, B = 1 }" },
        { "HENUM005", "required custom field missing", """[FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.CustomFieldRequired, DefaultToStringCustomField = "Code")] public enum E { [FancyEnumMember("Code", "u")] Unknown, A }""" },
        { "HENUM006", "ambiguous parse token", """[FancyEnum, FancyEnumMemberMappingSettings("F", ParseFrom = true)] public enum E { Unknown, [FancyEnumMember("F", "same")] A, [FancyEnumMember("F", "same")] B }""" },
        { "HENUM007", "generic container", "public class Box<T> { [FancyEnum] public enum E { Unknown, A } }" },
        { "HENUM007", "private nested enum", "public class Box { [FancyEnum] private enum E { Unknown, A } }" },
        { "HENUM008", "TryFormat on non-string field", """[FancyEnum, FancyEnumMemberMappingSettings<int>("F", CreateTryFormat = true)] public enum E { Unknown, [FancyEnumMember<int>("F", 1)] A }""" },
        { "HENUM008", "TryFormat with static source", """
            public static class Src { public static string V() => "v"; }
            [FancyEnum, FancyEnumMemberMappingSettings("F", CreateTryFormat = true)] public enum E { Unknown, [FancyEnumMember<string>("F", StaticMethodName = "V", StaticMethodSource = typeof(Src))] A }
            """ },
        { "HENUM009", "parse from non-string field", """[FancyEnum, FancyEnumMemberMappingSettings<int>("F", ParseFrom = true)] public enum E { Unknown, [FancyEnumMember<int>("F", 1)] A }""" },
        { "HENUM009", "parse from static source", """
            public static class Src { public static string V() => "v"; }
            [FancyEnum, FancyEnumMemberMappingSettings("F", ParseFrom = true)] public enum E { Unknown, [FancyEnumMember<string>("F", StaticMethodName = "V", StaticMethodSource = typeof(Src))] A }
            """ },
        { "HENUM010", "non-single-bit flag", "[System.Flags, FancyEnum(AllowNoUnknown = true)] public enum E { None = 0, A = 1, B = 2, AB = 3 }" },
        { "HENUM011", "UTF-8 on non-string field", """[FancyEnum, FancyEnumMemberMappingSettings<int>("F", IncludeUtf8Value = true)] public enum E { Unknown, [FancyEnumMember<int>("F", 1)] A }""" },
        { "HENUM011", "UTF-8 with static source", """
            public static class Src { public static string V() => "v"; }
            [FancyEnum, FancyEnumMemberMappingSettings("F", IncludeUtf8Value = true)] public enum E { Unknown, [FancyEnumMember<string>("F", StaticMethodName = "V", StaticMethodSource = typeof(Src))] A }
            """ },
        { "HENUM012", "default value of the wrong type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { [FancyEnumMemberSetItem(DefaultValue = 5)] public long Size { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Size = 1L)] A }
            """ },
        { "HENUM012", "null default for a value type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { [FancyEnumMemberSetItem(DefaultValue = null)] public int Size { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Size = 1)] A }
            """ },
        { "HENUM012", "NotMatched of the wrong type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { [FancyEnumMemberSetItem(NotMatched = "none")] public int Size { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Size = 1)] A }
            """ },
        { "HENUM013", "unresolvable constructor parameter", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute(string text) : System.Attribute { public string Label { get; } = text; }
            [FancyEnum] public enum E { Unknown, [Meta("a")] A }
            """ },
        { "HENUM013", "name matches but type doesn't", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute(int label) : System.Attribute { public string Label { get; } = label.ToString(); }
            [FancyEnum] public enum E { Unknown, [Meta(1)] A }
            """ },
        { "HENUM014", "member-set property named like a generated member", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public int Length { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Length = 1)] A }
            """ },
        { "HENUM014", "field named like a generated member", """[FancyEnum] public enum E { Unknown, [FancyEnumMember("Values", "v")] A }""" },
        { "HENUM014", "field named like a System.Enum member", """[FancyEnum] public enum E { Unknown, [FancyEnumMember("ToString", "v")] A }""" },
        { "HENUM014", "UTF-8 property collides with another field", """[FancyEnum, FancyEnumMemberMappingSettings("Label", IncludeUtf8Value = true)] public enum E { Unknown, [FancyEnumMember("Label", "a"), FancyEnumMember("LabelBytes", "b")] A }""" },
        { "HENUM014", "TryFormat length constant collides with another field", """[FancyEnum, FancyEnumMemberMappingSettings("Code", CreateTryFormat = true)] public enum E { Unknown, [FancyEnumMember("Code", "a"), FancyEnumMember("Code_LongestCharLength", "b")] A }""" },
        { "HENUM015", "property type can't be an attribute argument", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public System.DateTime When { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta] A }
            """ },
        { "HENUM015", "nullable value type property", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public int? Size { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta] A }
            """ },
        { "HENUM015", "array property", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public int[]? Sizes { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Sizes = new[] { 1 })] A }
            """ },
        { "HENUM015", "read-only property no constructor feeds", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public string Label { get; } = "fixed"; }
            [FancyEnum] public enum E { Unknown, [Meta] A }
            """ },
    };

    [Theory]
    [MemberData(nameof(Triggers))]
    public void Reports(string expectedId, string because, string source)
    {
        var run = GeneratorHarness.Run(Usings + source);
        Assert.True(run.GeneratorDiagnostics.Any(diagnostic => diagnostic.Id == expectedId),
            $"Expected {expectedId} ({because}); got [{string.Join(", ", run.GeneratorDiagnostics.Select(static diagnostic => diagnostic.Id))}]");
    }

    public static TheoryData<string, string> ValidLookAlikes => new()
    {
        { "Unknown matched case-insensitively", "[FancyEnum] public enum E { UNKNOWN = 0, A = 1 }" },
        { "AllowNoUnknown", "[FancyEnum(AllowNoUnknown = true)] public enum E { A = 0, B = 1 }" },
        { "AllowNonContiguous", "[FancyEnum(AllowNonContiguous = true)] public enum E { Unknown = 0, A = 1, B = 5 }" },
        { "flags enums allow non-contiguous values by default", "[System.Flags, FancyEnum] public enum E { Unknown = 0, A = 1, B = 2, C = 4, D = 8 }" },
        { "composite flag excluded from Values", "[System.Flags, FancyEnum(AllowNoUnknown = true)] public enum E { None = 0, A = 1, B = 2, [FancyEnumMemberSettings(ExcludeFromValues = true)] AB = 3 }" },
        { "same parse token on one member's name and value", """[FancyEnum, FancyEnumMemberMappingSettings("F", ParseFrom = true, NotDefined = FancyEnumMemberFallbackOption.NameOf)] public enum E { Unknown, [FancyEnumMember("F", "A")] A, B }""" },
        { "constructor parameter matched by name and type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute(string label) : System.Attribute { public string Label { get; } = label; }
            [FancyEnum] public enum E { Unknown, [Meta("a")] A }
            """ },
        { "constructor parameter mapped explicitly", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute([FancyEnumConstructorMapping("Label")] string text) : System.Attribute { public string Label { get; } = text; }
            [FancyEnum] public enum E { Unknown, [Meta("a")] A }
            """ },
        { "null default for a reference type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { [FancyEnumMemberSetItem(DefaultValue = null)] public string? Label { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Label = "a")] A }
            """ },
        { "explicit null mapping value", """[FancyEnum] public enum E { Unknown, [FancyEnumMember<string>("F", null)] A, [FancyEnumMember("F", "b")] B }""" },
        { "ignored property of an unsupported type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public string? Label { get; set; } [FancyEnumMemberSetItem(Ignore = true)] public System.DateTime When { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Label = "a")] A }
            """ },
        { "Type, object and enum properties", """
            public enum Shade { Dark = -1, Light = 1 }
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public System.Type? Handler { get; set; } public object? Extra { get; set; } public Shade Shade { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Handler = typeof(string), Extra = 1.5, Shade = Shade.Dark)] A, [Meta(Extra = Shade.Light)] B }
            """ },
        { "inherited property", """
            public abstract class BaseAttribute : System.Attribute { public string? Label { get; set; } }
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : BaseAttribute { }
            [FancyEnum] public enum E { Unknown, [Meta(Label = "a")] A }
            """ },
        { "NotMatched of the property's own type", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { [FancyEnumMemberSetItem(NotMatched = -1)] public int Size { get; set; } }
            [FancyEnum] public enum E { Unknown, [Meta(Size = 1)] A, B }
            """ },
        { "read-only property fed by a constructor", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute(string label) : System.Attribute { public string Label { get; } = label; }
            [FancyEnum] public enum E { Unknown, [Meta("a")] A }
            """ },
        { "negative enum and non-finite floating-point mapping values", """
            public enum Shade { Dark = -1, Light = 1 }
            [FancyEnum] public enum E { Unknown, [FancyEnumMember<Shade>("Shade", Shade.Dark), FancyEnumMember<double>("D", double.NaN), FancyEnumMember<float>("F", 0.1f)] A, [FancyEnumMember<double>("D", double.NegativeInfinity)] B }
            """ },
        { "large enum with names differing only by case", "[FancyEnum(CreateByteParsing = true)] public enum E { Unknown, Alpha, ALPHA, Bravo, Charlie, Delta, EchoEchoEcho, ECHOECHOECHO, Foxtrot }" },
        { "explicit enum settings override a member-set field", """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public string? Label { get; set; } }
            [FancyEnum, FancyEnumMemberMappingSettings("Label", ParseFrom = true)] public enum E { Unknown, [Meta(Label = "a")] A }
            """ },
    };

    [Theory]
    [MemberData(nameof(ValidLookAlikes))]
    public void StaysQuiet(string because, string source)
    {
        _ = because;
        var run = GeneratorHarness.Run(Usings + source);
        run.AssertNoGeneratorDiagnostics();
        run.AssertCompilesCleanly();
    }

    private const string BrokenShape = """
        [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
        public sealed class MetaAttribute(string text) : System.Attribute { public string Label { get; } = text; }
        """;

    [Fact]
    public void ShapeProblemsAreReportedOnceNoMatterHowManyEnumsUseIt()
    {
        var run = GeneratorHarness.Run(Usings + BrokenShape + """
            [FancyEnum] public enum E1 { Unknown, [Meta("a")] A }
            [FancyEnum] public enum E2 { Unknown, [Meta("b")] B }
            """);
        Assert.Single(run.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "HENUM013");
    }

    [Fact]
    public void ShapeProblemsAreReportedEvenBeforeAnyEnumUsesIt()
    {
        var run = GeneratorHarness.Run(Usings + BrokenShape);
        Assert.Single(run.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "HENUM013");
    }

    [Fact]
    public void CollidingFieldIsSkippedSoTheRestStillCompiles()
    {
        var run = GeneratorHarness.Run(Usings + """[FancyEnum] public enum E { Unknown, [FancyEnumMember("Values", "v"), FancyEnumMember("Label", "l")] A }""");
        Assert.Contains(run.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "HENUM014");
        run.AssertCompilesCleanly();
        Assert.Contains("Label", run.GeneratedSource("E.FancyEnum.g.cs"));
    }

    [Fact]
    public async Task WarnsWhenAMemberSetIsUsedWithoutFancyEnum()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzersAsync(Usings + """
            [FancyEnumMemberSet, System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class MetaAttribute : System.Attribute { public string? Label { get; set; } }
            public enum Forgotten { Unknown, [Meta(Label = "a")] A }
            [FancyEnum] public enum Remembered { Unknown, [Meta(Label = "a")] A }
            public enum Unrelated { Unknown, [System.Obsolete] A }
            """);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HENUM016", diagnostic.Id);
        Assert.Contains("Forgotten", diagnostic.GetMessage());
    }

    /// <remarks>
    /// Generator diagnostics carry an equatable file/line location rather than a SyntaxTree-bound one (a live
    /// Location would defeat incremental caching), so this checks the file and line rather than IsInSource.
    /// </remarks>
    [Fact]
    public void DiagnosticsPointAtTheOffendingMember()
    {
        var run = GeneratorHarness.Run(Usings + """
            [FancyEnum]
            public enum E
            {
                Unknown = 0,
                A = 1,
                B = 1,
            }
            """);
        var diagnostic = Assert.Single(run.GeneratorDiagnostics, static diagnostic => diagnostic.Id == "HENUM004");
        var span = diagnostic.Location.GetLineSpan();
        Assert.Equal("Source0.cs", span.Path);
        Assert.Equal(6, span.StartLinePosition.Line + 1); // `A = 1,`: the first member of the duplicate group.
    }
}
