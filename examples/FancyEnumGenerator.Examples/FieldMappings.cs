// README: "Field mappings" and "Fallback behavior for missing mappings".
// See Generated/.../Beverage.FancyEnum.g.cs.
using FancyEnumGenerator.Attributes;

// CreateStaticReadonlyCollection = true: opts into a cached Values/AsSpan pair (built once, lazily, on
// first access) instead of the default (Values freshly built on every access, no AsSpan at all - see
// Fruit in BasicUsage.cs for that default).
[FancyEnum(CreateStaticReadonlyCollection = true)]
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
