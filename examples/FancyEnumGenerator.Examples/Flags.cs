// README: "Flags enums".
// See Generated/.../Permission.FancyEnum.g.cs.
using FancyEnumGenerator.Attributes;

[Flags]
[FancyEnum(CreateTryFormat = true)]
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

// An obsolete flag in the middle: excluded from the generated code by default (IncludeObsolete = false), so its bit
// is no longer a declared flag. The gap it leaves doesn't need AllowNonContiguous: contiguity is judged on every
// declared value, and the other flags keep working, alone or combined.
// See Generated/.../FlagWithObsolete.FancyEnum.g.cs.
[Flags]
[FancyEnum(CreateTryFormat = true)]
public enum FlagWithObsolete
{
    Unknown = 0,
    Search = 1,

    [Obsolete("Use Search")]
    LegacySearch = 2,

    Export = 4,
    Sharing = 8,
}
