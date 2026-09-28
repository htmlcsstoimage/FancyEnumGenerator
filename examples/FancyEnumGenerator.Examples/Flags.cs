// README: "Flags enums".
// See Generated/.../Permission.FancyEnum.g.cs.
using FancyEnumGenerator.Attributes;

[Flags]
[FancyEnum]
public enum Permission
{
    Unknown = 0,
    Read = 1,
    Write = 2,
    Execute = 4,

    // A composite/derived value: excluded from Values/AsSpan and from the "not a single bit" warning.
    [FancyEnumMemberSettings(ExcludeFromValues = true)]
    All = Read | Write | Execute,
}
