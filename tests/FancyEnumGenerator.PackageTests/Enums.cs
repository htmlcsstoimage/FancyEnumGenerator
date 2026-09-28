using System;
using FancyEnumGenerator.Attributes;

namespace Smoke;

/// <summary>Default settings.</summary>
[FancyEnum]
public enum Fruit
{
    /// <summary>Not a fruit.</summary>
    Unknown = 0,
    /// <summary>An apple.</summary>
    Apple = 1,
    /// <summary>A banana.</summary>
    Banana = 2,
}

/// <summary>Flags, formatted as combinations.</summary>
[Flags]
[FancyEnum(AllowNoUnknown = true, CreateTryFormat = true)]
public enum Permission
{
    /// <summary>No permissions.</summary>
    None = 0,
    /// <summary>Read.</summary>
    Read = 1,
    /// <summary>Write.</summary>
    Write = 2,
    /// <summary>Execute.</summary>
    Execute = 4,
}

/// <summary>
/// Enough names of one length (5 and 17 characters) that text parsing uses per-length helpers and UTF-8 parsing uses
/// packed-ASCII switches, with every other option on.
/// </summary>
[FancyEnum(CreateByteParsing = true, CreateTryFormat = true, CreateStaticReadonlyCollection = true, CreateIsValidPrefix = true, ParseCaseSensitive = false)]
[FancyEnumMemberMappingSettings("Code", IncludeUtf8Value = true, ParseFrom = true, CreateTryFormat = true)]
public enum Region
{
    /// <summary>Unknown.</summary>
    Unknown = 0,
    /// <summary>Alpha.</summary>
    [FancyEnumMember("Code", "A1")] Alpha,
    /// <summary>Bravo.</summary>
    [FancyEnumMember("Code", "B2")] Bravo,
    /// <summary>Delta.</summary>
    [FancyEnumMember("Code", "D3")] Delta,
    /// <summary>Gamma.</summary>
    [FancyEnumMember("Code", "G4")] Gamma,
    /// <summary>Omega.</summary>
    [FancyEnumMember("Code", "O5")] Omega,
    /// <summary>Sigma.</summary>
    [FancyEnumMember("Code", "S6")] Sigma,
    /// <summary>West coast 1.</summary>
    WestCoastRegion01,
    /// <summary>West coast 2.</summary>
    WestCoastRegion02,
    /// <summary>West coast 3.</summary>
    WestCoastRegion03,
    /// <summary>West coast 4.</summary>
    WestCoastRegion04,
    /// <summary>West coast 5.</summary>
    WestCoastRegion05,
}

/// <summary>A member set.</summary>
[FancyEnumMemberSet]
[AttributeUsage(AttributeTargets.Field)]
public sealed class InfoAttribute : Attribute
{
    /// <summary>The label.</summary>
    public string? Label { get; set; }

    /// <summary>The display order.</summary>
    [FancyEnumMemberSetItem(DefaultValue = -1)]
    public int Order { get; set; }
}

/// <summary>Uses the member set.</summary>
[FancyEnum]
public enum Planet
{
    /// <summary>Unknown.</summary>
    Unknown = 0,
    /// <summary>Mars.</summary>
    [Info(Label = "red", Order = 40)] Mars = 1,
    /// <summary>Venus.</summary>
    Venus = 2,
}
