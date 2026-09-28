using Microsoft.CodeAnalysis;

namespace FancyEnumGenerator.Internal;

internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    GeneratorLocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params string[] messageArgs)
        : this(descriptor, GeneratorLocationInfo.FromLocation(location), new EquatableArray<string>(messageArgs))
    {
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params object?[] messageArgs)
    {
        return Create(descriptor, GeneratorLocationInfo.FromLocation(location), messageArgs);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, GeneratorLocationInfo? location, params object?[] messageArgs)
    {
        return new DiagnosticInfo(descriptor, location, messageArgs.Select(static argument => argument?.ToString() ?? "").ToEquatableArray());
    }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, MessageArgs.GetArray() ?? Array.Empty<string>());
    }
}
