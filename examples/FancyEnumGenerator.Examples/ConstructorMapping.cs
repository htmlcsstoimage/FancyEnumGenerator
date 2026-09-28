// README: "Read-only properties and constructors".
// See Generated/.../Widget.FancyEnum.g.cs.
using FancyEnumGenerator.Attributes;

// Auto-matched: constructor parameter "className" matches property "ClassName" by name
// (case-insensitive) and type - no mapping attribute needed.
[FancyEnumMemberSet]
public sealed class CssClassAttribute : Attribute
{
    public string ClassName { get; }

    public CssClassAttribute(string className)
    {
        ClassName = className;
    }
}

// Explicit mapping: the parameter name "css" doesn't match "ClassName", so it must be marked.
[FancyEnumMemberSet]
public sealed class ExplicitCssClassAttribute : Attribute
{
    public string ClassName { get; }

    public ExplicitCssClassAttribute([FancyEnumConstructorMapping(nameof(ClassName))] string css)
    {
        ClassName = css;
    }
}

// The documented limitation: the constructor transforms its argument (order + 1) before assigning.
// The generator only ever sees the raw value as written (10), never the transformed one (11).
[FancyEnumMemberSet]
public sealed class OrderAttribute : Attribute
{
    public int Order { get; }

    public OrderAttribute(int order)
    {
        Order = order + 1;
    }
}

[FancyEnum]
public enum Widget
{
    Unknown = 0,

    [CssClass("btn-primary")]
    Sprocket = 1,

    [ExplicitCssClass("btn-secondary")]
    Cog = 2,

    [Order(10)]
    Gear = 3,
}
