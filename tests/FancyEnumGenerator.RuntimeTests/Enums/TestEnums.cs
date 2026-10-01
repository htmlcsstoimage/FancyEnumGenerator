using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using FancyEnumGenerator.Attributes;

namespace FancyEnumGenerator.RuntimeTests.Enums;

[FancyEnum]
public enum Fruit
{
    Unknown = 0,
    Apple = 1,
    Banana = 2,
    Cherry = 3,
}

[Flags]
[FancyEnum(AllowNoUnknown = true, CreateTryFormat = true)]
public enum Permission
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    [FancyEnumMemberSettings(ExcludeFromValues = true)]
    ReadWrite = Read | Write,
}

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
    Water = 5,
}

public enum Shade { Dark = -1, Light = 1 }

public sealed class CarrotHandler { }

public abstract class TaggedAttribute : Attribute
{
    public string? Tag { get; set; }
}

[FancyEnumMemberSet]
[AttributeUsage(AttributeTargets.Field)]
public sealed class VegetableMetadataAttribute : TaggedAttribute
{
    public VegetableMetadataAttribute() { }
    public VegetableMetadataAttribute(string label) => Label = label;

    [FancyEnumMemberSetItem(ParseFrom = true)]
    public string? Label { get; set; }

    [FancyEnumMemberSetItem(Name = "SortOrder", DefaultValue = -1)]
    public int Order { get; set; }

    [FancyEnumMemberSetItem(Ignore = true)]
    public bool Hidden { get; set; }

    public Type? Handler { get; set; }
    public object? Extra { get; set; }
    public Shade Shade { get; set; }
}

[FancyEnumMemberSet]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CssClassAttribute([FancyEnumConstructorMapping(nameof(CssClassAttribute.ClassName))] string css) : Attribute
{
    public string ClassName { get; } = css;
}

[FancyEnumMemberSet]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CssValueAttribute(string cssValue) : Attribute
{
    [FancyEnumMemberSetItem(NotDefined = FancyEnumMemberFallbackOption.NameOfLower, ParseFrom = true, CreateTryFormat = true, IncludeUtf8Value = true)]
    public string CssValue { get; } = cssValue;
}

[FancyEnum]
public enum Alignment
{
    Unknown = 0,
    [CssValue("flex-start")] Start = 1,
    Center = 2, // no CssValue: falls back to "center"
    [CssValue("flex-end")] End = 3,
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

[FancyEnum(ValuesType = FancyEnumValuesType.StaticCollection)]
public enum Cached { Unknown, A, B, [FancyEnumMemberSettings(ExcludeFromValues = true)] Hidden, C }

[FancyEnum(NoInlineArray = true, ValuesType = FancyEnumValuesType.StaticCollection)]
public enum PlainArray { Unknown, A, B }

[FancyEnum]
public enum Spanned : long { Unknown, A, B, [FancyEnumMemberSettings(ExcludeFromValues = true)] Hidden, C }

[FancyEnum(ValuesType = FancyEnumValuesType.InlineArray)]
public enum Inline { Unknown, A, B, [FancyEnumMemberSettings(ExcludeFromValues = true)] Hidden, C }

/// <summary>Enough same-length and long tokens to exercise every parser strategy (see the generator's Parsing scenario).</summary>
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

/// <summary>Non-ASCII names, and names differing only by case, in an enum large enough for the switch-based parsers.</summary>
[FancyEnum(CreateByteParsing = true)]
public enum Accents
{
    Unknown = 0,
    Café = 1,
    Cafe = 2,
    Naïve = 3,
    Résumé = 4,
    Alpha = 5,
    ALPHA = 6,
}

/// <summary>
/// Case-insensitive by default, with wire names that differ from the member names only by case (Te/"TE"): the
/// explicit case-sensitive overload must still match each spelling exactly. Two copies, so both the direct-comparison
/// path (few tokens) and the switch path (many) are covered.
/// </summary>
[FancyEnum(ParseCaseSensitive = false, DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.CustomFieldFallback, DefaultToStringCustomField = "Wire")]
public enum FewWireNames { Unknown, [FancyEnumMember("Wire", "TE")] Te }

[FancyEnum(ParseCaseSensitive = false, DefaultToStringBehavior = FancyEnumDefaultToStringBehavior.CustomFieldFallback, DefaultToStringCustomField = "Wire")]
public enum ManyWireNames
{
    Unknown,
    [FancyEnumMember("Wire", "TE")] Te,
    [FancyEnumMember("Wire", "ETAG")] ETag,
    [FancyEnumMember("Wire", "X-Id")] XId,
    [FancyEnumMember("Wire", "VIA")] Via,
    Age,
    Date,
}

[FancyEnumMemberSet]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CodeInfoAttribute : Attribute
{
    [FancyEnumMemberSetItem(ParseFrom = true)]
    public string? Tag { get; set; }
}

/// <summary>Field parsers inherit the enum's ParseCaseSensitive unless they set their own.</summary>
[FancyEnum(ParseCaseSensitive = false)]
[FancyEnumMemberMappingSettings("Code", ParseFrom = true)]
[FancyEnumMemberMappingSettings("StrictCode", ParseFrom = true, ParseCaseSensitive = true)]
public enum InheritsCase
{
    Unknown,
    [FancyEnumMember("Code", "ab"), FancyEnumMember("StrictCode", "xy"), CodeInfo(Tag = "tg")] First,
}

[FancyEnum(AllowNoUnknown = true, AllowNonContiguous = true)]
public enum Sparse : short
{
    Negative = -5,
    Zero = 0,
    Seven = 7,
    Hundred = 100,
}

[FancyEnum]
public enum WithObsolete
{
    Unknown,
    Current,
    [Obsolete] Legacy,
}

/// <summary>Flags whose defined-bits mask needs care: promotion to int for byte, the sign bit, the top bit of a ulong.</summary>
[Flags]
[FancyEnum(AllowNoUnknown = true, CreateTryFormat = true)]
public enum ByteFlags : byte { None = 0, Low = 1, High = 128 }

[Flags]
[FancyEnum(AllowNoUnknown = true, CreateTryFormat = true)]
public enum SignFlags : sbyte { None = 0, Low = 1, Sign = sbyte.MinValue }

[Flags]
[FancyEnum(AllowNoUnknown = true, CreateTryFormat = true)]
public enum WideFlags : ulong { None = 0, Low = 1, Top = 1UL << 63 }

/// <summary>Flags with an Unknown member, a declared composite, and a declared member no single-bit flag covers.</summary>
[Flags]
[FancyEnum]
public enum FlagsWithUnknown
{
    Unknown = 0,
    A = 1,
    B = 2,
    C = 4,
    [FancyEnumMemberSettings(ExcludeFromValues = true)] AB = A | B,
    [FancyEnumMemberSettings(ExcludeFromValues = true)] Weird = 24,
}

/// <summary>An excluded obsolete member in the middle leaves a gap; that mustn't need AllowNonContiguous.</summary>
[FancyEnum]
public enum ObsoleteInTheMiddle
{
    Unknown,
    First,
    [Obsolete] Middle,
    Last,
}

[FancyEnum(IncludeObsolete = true)]
public enum KeepsObsolete
{
    Unknown,
    Current,
    [Obsolete("Use Current")] Older,
}

public static partial class Outer
{
    [FancyEnum]
    public enum Nested : byte { Unknown, Only }
}
