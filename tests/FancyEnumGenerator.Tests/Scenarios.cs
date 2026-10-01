namespace FancyEnumGenerator.Tests;

/// <summary>A named consumer source file exercising one area of the generator.</summary>
/// <remarks>
/// Sources deliberately have no <c>using System;</c> and the harness adds no implicit usings, so every scenario
/// also proves the generated code doesn't depend on the consumer's usings.
/// </remarks>
public sealed record Scenario
{
    public required string Name { get; init; }
    public required string Source { get; init; }

    /// <summary>Uses an API that only exists on modern .NET (e.g. JsonStringEnumMemberNameAttribute), so it's skipped for netstandard2.0.</summary>
    public bool Net10Only { get; init; }

    public override string ToString() => Name;
}

public static class Scenarios
{
    public static readonly Scenario Basic = new()
    {
        Name = nameof(Basic),
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            [FancyEnum]
            public enum Fruit
            {
                Unknown = 0,
                Apple = 1,
                Banana = 2,
                Cherry = 3,
            }
            """
    };

    public static readonly Scenario Flags = new()
    {
        Name = nameof(Flags),
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            [System.Flags]
            [FancyEnum(AllowNoUnknown = true, CreateTryFormat = true)]
            public enum Permission
            {
                None = 0,
                Read = 1,
                Write = 2,
                Execute = 4,
                [FancyEnumMemberSettings(ExcludeFromValues = true)]
                All = Read | Write | Execute,
            }
            """
    };

    public static readonly Scenario FieldMappings = new()
    {
        Name = nameof(FieldMappings),
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            public static class BeverageSources
            {
                public static readonly string Origin = "imported";
                public static string Computed() => "computed";
            }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.CustomFieldFallback, DefaultToStringCustomField = "Label")]
            [FancyEnumMemberMappingSettings("Label", ParseFrom = true, CreateTryFormat = true, IncludeUtf8Value = true, NotMatched = "?")]
            [FancyEnumMemberMappingSettings("Slug", NotDefined = FancyEnumMemberFallbackOption.NameOfLower, ParseFrom = true, ParseCaseSensitive = false)]
            [FancyEnumMemberMappingSettings("Emoji", ReturnNullOnNotMatched = true)]
            [FancyEnumMemberMappingSettings<int>("Calories", ThrowOnNotMatched = true)]
            public enum Beverage
            {
                Unknown = 0,
                [FancyEnumMember("Label", "Hot Coffee"), FancyEnumMember<int>("Calories", 5), FancyEnumMember("Emoji", "coffee-cup")]
                Coffee = 1,
                [FancyEnumMember("Label", "Green Tea"), FancyEnumMember<int>("Calories", 2)]
                Tea = 2,
                [FancyEnumMember<int>("Calories", 150), FancyEnumMember<string>("Source", StaticMethodName = nameof(BeverageSources.Computed), StaticMethodSource = typeof(BeverageSources))]
                Juice = 3,
                [FancyEnumMember("Label", "Sparkling"), FancyEnumMember("Slug", "fizzy"), FancyEnumMember<string>("Source", StaticMethodName = nameof(BeverageSources.Origin), StaticMethodSource = typeof(BeverageSources))]
                Soda = 4,
                [FancyEnumMember<int>("Calories", 0)]
                Water = 5,
            }
            """
    };

    public static readonly Scenario MemberSet = new()
    {
        Name = nameof(MemberSet),
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            public enum Shade { Dark = -1, Light = 1 }

            public sealed class CarrotHandler { }

            public abstract class TaggedAttribute : System.Attribute
            {
                public string? Tag { get; set; }
            }

            [FancyEnumMemberSet]
            [System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class VegetableMetadataAttribute : TaggedAttribute
            {
                public VegetableMetadataAttribute() { }
                public VegetableMetadataAttribute(string label) => Label = label;

                [FancyEnumMemberSetItem(ParseFrom = true, CreateTryFormat = true)]
                public string? Label { get; set; }

                [FancyEnumMemberSetItem(Name = "SortOrder", DefaultValue = -1)]
                public int Order { get; set; }

                [FancyEnumMemberSetItem(Ignore = true)]
                public bool Hidden { get; set; }

                [FancyEnumMemberSetItem(Ignore = true)]
                public System.DateTime NotAnAttributeArgument { get; set; }

                public System.Type? Handler { get; set; }
                public object? Extra { get; set; }
                public Shade Shade { get; set; }
            }

            [FancyEnumMemberSet]
            [System.AttributeUsage(System.AttributeTargets.Field)]
            public sealed class CssClassAttribute([FancyEnumConstructorMapping("ClassName")] string css) : System.Attribute
            {
                public string ClassName { get; } = css;
            }

            [FancyEnum]
            public enum Vegetable
            {
                Unknown = 0,
                [VegetableMetadata("carrot", Order = 1, Tag = "root", Handler = typeof(CarrotHandler), Extra = 'x', Shade = Shade.Dark), CssClass("veg-orange")]
                Carrot = 1,
                [VegetableMetadata(Label = "pea", Hidden = true, Extra = Shade.Light)]
                Pea = 2,
                Leek = 3,
            }
            """
    };

    public static readonly Scenario ToStringAttributes = new()
    {
        Name = nameof(ToStringAttributes),
        Net10Only = true,
        Source = """
            using System.ComponentModel;
            using System.ComponentModel.DataAnnotations;
            using System.Runtime.Serialization;
            using System.Text.Json.Serialization;
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.DescriptionAttribute)]
            public enum ByDescription { Unknown, [Description("First one")] First, Second }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.DisplayAttribute)]
            public enum ByDisplay { Unknown, [Display(Name = "First one")] First, Second }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.EnumMemberAttribute)]
            public enum ByEnumMember { Unknown, [EnumMember(Value = "first_one")] First, Second }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.JsonStringEnumMemberNameAttribute)]
            public enum ByJsonName { Unknown, [JsonStringEnumMemberName("first-one")] First, Second }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.NameOfLower)]
            public enum ByLower { Unknown, FirstOne, SecondOne }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.NameOfUpper)]
            public enum ByUpper { Unknown, FirstOne, SecondOne }

            [FancyEnum(DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.CustomFieldRequired, DefaultToStringCustomField = "Code")]
            public enum ByRequiredField { [FancyEnumMember("Code", "?")] Unknown, [FancyEnumMember("Code", "F1")] First, [FancyEnumMember("Code", "S2")] Second }
            """
    };

    public static readonly Scenario Collections = new()
    {
        Name = nameof(Collections),
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            [FancyEnum(ValuesType = FancyEnumValuesType.StaticCollection)]
            public enum Cached { Unknown, A, B, [FancyEnumMemberSettings(ExcludeFromValues = true)] Hidden, C }

            [FancyEnum(NoInlineArray = true, ValuesType = FancyEnumValuesType.StaticCollection)]
            public enum PlainArray { Unknown, A, B }

            [FancyEnum]
            public enum Spanned { Unknown, A, B, [FancyEnumMemberSettings(ExcludeFromValues = true)] Hidden, C }

            [FancyEnum(ValuesType = FancyEnumValuesType.Span)]
            public enum SmallSpan : byte { Unknown, A, B }

            [FancyEnum(NoInlineArray = true)]
            public enum NoCollection { Unknown, A, B }
            """
    };

    /// <summary>ValuesType = InlineArray needs .NET 8+, so this one only builds there (downlevel it reports HENUM017 instead).</summary>
    public static readonly Scenario InlineArrayValues = new()
    {
        Name = nameof(InlineArrayValues),
        Net10Only = true,
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            [FancyEnum(ValuesType = FancyEnumValuesType.InlineArray)]
            public enum Inline { Unknown, A, B, [FancyEnumMemberSettings(ExcludeFromValues = true)] Hidden, C }

            [System.Flags]
            [FancyEnum(ValuesType = FancyEnumValuesType.InlineArray, AllowNoUnknown = true)]
            public enum InlineFlags : long { None = 0, A = 1, B = 2, C = 1L << 40 }
            """
    };

    /// <summary>Enough same-length and long (&gt; 8 and &gt; 16 char) tokens to exercise every parser strategy: direct compares, the length switch, and packed-ASCII hashing with a tail compare.</summary>
    public static readonly Scenario Parsing = new()
    {
        Name = nameof(Parsing),
        Source = """
            using FancyEnumGenerator.Attributes;

            namespace Demo;

            [FancyEnum(CreateByteParsing = true, CreateTryFormat = true, CreateIsValidPrefix = true, ParseCaseSensitive = false)]
            public enum Station
            {
                Unknown = 0,
                EastCoast1 = 1,
                EastCoast2 = 2,
                EastCoast3 = 3,
                EastCoast4 = 4,
                EastCoast5 = 5,
                WestCoastRegion01 = 6,
                WestCoastRegion02 = 7,
                WestCoastRegion03 = 8,
                WestCoastRegion04 = 9,
                WestCoastRegion05 = 10,
                Hub = 11,
            }
            """
    };

    public static readonly Scenario Shapes = new()
    {
        Name = nameof(Shapes),
        Source = """
            using FancyEnumGenerator.Attributes;

            [FancyEnum(AllowNoUnknown = true, AllowNonContiguous = true)]
            public enum GlobalSparse : short
            {
                Negative = -5,
                Zero = 0,
                Seven = 7,
                Hundred = 100,
            }

            namespace Demo
            {
                public static partial class Outer
                {
                    public static partial class Inner
                    {
                        [FancyEnum]
                        public enum Nested : byte { Unknown, Only }
                    }
                }

                [FancyEnum]
                internal enum InternalEnum : long { Unknown, Big = 1 }

                [FancyEnum]
                public enum WithObsolete
                {
                    Unknown,
                    Current,
                    [System.Obsolete] Legacy,
                }

                [FancyEnum(IncludeObsolete = true)]
                public enum KeepsObsolete
                {
                    Unknown,
                    Current,
                    [System.Obsolete] Legacy,
                    [System.Obsolete("Use Current")] Older,
                }
            }
            """
    };

    public static readonly IReadOnlyList<Scenario> All = [Basic, Flags, FieldMappings, MemberSet, ToStringAttributes, Collections, InlineArrayValues, Parsing, Shapes];
}
