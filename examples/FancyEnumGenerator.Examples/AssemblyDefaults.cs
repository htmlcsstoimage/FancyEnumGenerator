// README: "Assembly-wide: [assembly: FancyEnumDefaults(...)]".
// See Generated/.../Gadget.FancyEnum.g.cs.
// Kept deliberately non-destructive at the assembly level (only settings every other example in
// this project can safely inherit too) - see AssemblyInfo.cs for the declaration itself.
using FancyEnumGenerator.Attributes;

// No AllowNoUnknown here at all, and deliberately no "Unknown" member - relies purely on the
// assembly-wide default declared in AssemblyInfo.cs.
[FancyEnum]
public enum Gadget
{
    Sprocket = 1,
    Cog = 2,
}
