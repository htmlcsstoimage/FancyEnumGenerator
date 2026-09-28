using Microsoft.CodeAnalysis;

namespace FancyEnumGenerator;

internal static class EnumGeneratorDiagnostics
{
    public static readonly DiagnosticDescriptor MissingUnknown = Create("HENUM001", "Missing unknown member", "Enum '{0}' must define an unknown member (case-insensitive) with numeric value zero or set AllowNoUnknown");
    public static readonly DiagnosticDescriptor NonContiguous = Create("HENUM002", "Non-contiguous enum values", "Enum '{0}' has non-contiguous numeric values and must set AllowNonContiguous");
    public static readonly DiagnosticDescriptor InvalidMapping = Create("HENUM003", "Invalid enum mapping", "Enum '{0}' has an invalid mapping for field '{1}': {2}");
    public static readonly DiagnosticDescriptor DuplicateNumericValue = Create("HENUM004", "Duplicate enum numeric value", "Enum '{0}' defines numeric value '{1}' more than once");
    public static readonly DiagnosticDescriptor MissingCustomStringMapping = Create("HENUM005", "Missing custom string mapping", "Enum member '{0}.{1}' must define string field '{2}'");
    public static readonly DiagnosticDescriptor AmbiguousParserToken = Create("HENUM006", "Ambiguous enum parser token", "Parser source '{0}' uses token '{1}' for multiple members; parsing will select '{2}'", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor UnsupportedEnumContainer = Create("HENUM007", "Enum container is unsupported", "Enum '{0}' cannot generate namespace-level extensions: {1}");
    public static readonly DiagnosticDescriptor TryFormatUnavailable = Create("HENUM008", "TryFormat generation is unavailable", "Mapping field '{0}' cannot generate TryFormat: {1}");
    public static readonly DiagnosticDescriptor ParseUnavailable = Create("HENUM009", "Parser generation is unavailable", "Mapping field '{0}' cannot generate a parser: {1}");
    public static readonly DiagnosticDescriptor InvalidFlagsValue = Create("HENUM010", "Flags member is not a single bit", "Flags enum '{0}' member '{1}' has value '{2}', which is neither zero nor a single-bit power of two", DiagnosticSeverity.Warning);
    public static readonly DiagnosticDescriptor Utf8ValueUnavailable = Create("HENUM011", "UTF-8 value generation is unavailable", "Mapping field '{0}' cannot generate a UTF-8 value: {1}");
    public static readonly DiagnosticDescriptor MemberSetValueTypeMismatch = Create("HENUM012", "Member-set value type mismatch", "Property '{0}.{1}' has type '{2}' but its FancyEnumMemberSetItem.{4} is of type '{3}'; the value is ignored");
    public static readonly DiagnosticDescriptor MemberSetConstructorParameterUnresolved = Create("HENUM013", "Member-set constructor parameter is unresolved", "Constructor parameter '{0}' on '{1}' does not match any property by name and type; add [FancyEnumConstructorMapping(nameof(YourProperty))]");
    public static readonly DiagnosticDescriptor ReservedFieldName = Create("HENUM014", "Mapped field name collides with a generated member", "Enum '{0}' field '{1}' would generate member '{2}', which {3}; rename the field");
    public static readonly DiagnosticDescriptor MemberSetPropertyUnusable = Create("HENUM015", "Member-set property can never be set", "Property '{0}.{1}' {2}; mark it [FancyEnumMemberSetItem(Ignore = true)] or change it");
    public static readonly DiagnosticDescriptor MemberSetWithoutFancyEnum = Create("HENUM016", "Member-set attribute on an enum without [FancyEnum]", "Enum '{0}' uses member-set attribute '{1}' but has no [FancyEnum], so nothing is generated for it; add [FancyEnum]", DiagnosticSeverity.Warning);

    private static DiagnosticDescriptor Create(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        return new DiagnosticDescriptor(id, title, message, "FancyEnum", severity, true);
    }
}
