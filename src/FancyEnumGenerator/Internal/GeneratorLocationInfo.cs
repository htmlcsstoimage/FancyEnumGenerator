using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FancyEnumGenerator.Internal;

internal readonly record struct GeneratorLocationInfo(
    string? FilePath,
    int Start,
    int Length,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn)
{
    public static GeneratorLocationInfo? FromLocation(Location? location)
    {
        if (location is null || location == Location.None || !location.IsInSource)
        {
            return null;
        }

        var sourceSpan = location.SourceSpan;
        var lineSpan = location.GetLineSpan();
        return new GeneratorLocationInfo(
            lineSpan.Path,
            sourceSpan.Start,
            sourceSpan.Length,
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character,
            lineSpan.EndLinePosition.Line,
            lineSpan.EndLinePosition.Character);
    }

    public Location ToLocation()
    {
        return Location.Create(
            FilePath ?? string.Empty,
            new TextSpan(Start, Length),
            new LinePositionSpan(
                new LinePosition(StartLine, StartColumn),
                new LinePosition(EndLine, EndColumn)));
    }
}
