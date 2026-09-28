// README: "FancyEnumMemberSet - bundle several fields into one attribute".
// See Generated/.../Vegetable.FancyEnum.g.cs.
using FancyEnumGenerator.Attributes;

[FancyEnumMemberSet]
public sealed class VegetableMetadataAttribute : Attribute
{
    public string? Label { get; set; }

    [FancyEnumMemberSetItem(Name = "SortOrder", DefaultValue = -1)]
    public int Order { get; set; }
}

[FancyEnum]
public enum Vegetable
{
    Unknown = 0,

    // Order is a display order, independent of the numeric value.
    [VegetableMetadata(Label = "carrot", Order = 30)]
    Carrot = 1,

    // No VegetableMetadata at all - Label and SortOrder both fall back per their own settings.
    Potato = 2,
}
