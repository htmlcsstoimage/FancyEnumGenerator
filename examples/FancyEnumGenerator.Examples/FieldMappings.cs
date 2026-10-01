// README: "Field mappings" and "Fallback behavior for missing mappings".
// See Generated/.../Beverage.FancyEnum.g.cs.
using FancyEnumGenerator.Attributes;

// ValuesType = StaticCollection: Values is an IReadOnlyList over an array created once and cached, so it can be
// stored or kept across an await, plus AsSpan over the same array. The default (see Fruit in BasicUsage.cs) is a
// ReadOnlySpan over static data instead.
[FancyEnum(ValuesType = FancyEnumValuesType.StaticCollection)]
[FancyEnumMemberMappingSettings("Label", NotDefined = FancyEnumMemberFallbackOption.NameOfLower)]
[FancyEnumMemberMappingSettings<int>("SortOrder", NotDefined = -1)]
public enum Beverage
{
    Unknown = 0,

    // SortOrder is a display order, deliberately independent of the numeric values: Tea (2) sorts before Coffee (1).
    [FancyEnumMember("Label", "coffee")]
    [FancyEnumMember<int>("SortOrder", 20)]
    Coffee = 1,

    [FancyEnumMember("Label", "tea")]
    [FancyEnumMember<int>("SortOrder", 10)]
    Tea = 2,

    // No mappings at all - Label falls back to "juice" (NameOfLower), SortOrder falls back to -1.
    Juice = 3,
}
