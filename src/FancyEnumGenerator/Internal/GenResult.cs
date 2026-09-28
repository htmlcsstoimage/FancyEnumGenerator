namespace FancyEnumGenerator.Internal;

internal sealed record GenResult
{
    public string SourceCode { get; init; } = "";
    public string? FileName { get; init; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}
