using System.Globalization;
using System.Text;

namespace FancyEnumGenerator;

public sealed partial class FancyEnumSourceGenerator
{
    private const int DirectParserComparisonThreshold = 4;
    private const string ParsingHelpersType = "global::FancyEnumGenerator.Generated.FancyEnumParsingHelpers";
    private const string EnumTypeAlias = "thisEnum";
    private const string CharSpanAlias = "CharSpan";
    private const string ByteSpanAlias = "ByteSpan";
    private const string StringComparisonAlias = "StringComparison";
    private const string InlineArrayAlias = "InlineArray";

    private static GenResult Emit(EnumModel model)
    {
        var diagnostics = model.Diagnostics.ToList();
        if (model.FullyQualifiedName.Length == 0 || diagnostics.Any(static diagnostic => diagnostic.Descriptor == EnumGeneratorDiagnostics.UnsupportedEnumContainer))
        {
            return new GenResult { Diagnostics = diagnostics.ToEquatableArray() };
        }

        var members = model.Members.GroupBy(static member => member.NumericValue).Select(static group => group.First()).OrderBy(static member => member.NumericValue).ToArray();
        var unknownMember = members.FirstOrDefault(IsUnknownMember);
        var hasUnknown = unknownMember is not null;
        var nonUnknownMembers = members.Where(member => !hasUnknown || member.NumericValue != 0).ToArray();
        var membersAreContiguous = AreContiguous(members);
        var nonUnknownMembersAreContiguous = AreContiguous(nonUnknownMembers);
        var arrayMembers = nonUnknownMembers.Where(static member => !member.ExcludeFromValues).ToArray();
        var firstNonUnknownExpression = nonUnknownMembers.Length > 0 ? $"{EnumTypeAlias}.{nonUnknownMembers[0].Name}" : "default";
        var firstNonUnknownOrdinal = nonUnknownMembers.Length > 0 ? nonUnknownMembers[0].NumericValue : 0;
        var unknownExpression = hasUnknown ? $"{EnumTypeAlias}.{unknownMember!.Name}" : "default";
        var fields = model.MappingSettings.Select(static setting => setting.FieldName)
            .Concat(model.Members.SelectMany(static member => member.Mappings).Select(static mapping => mapping.FieldName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static field => field, StringComparer.Ordinal)
            .ToArray();
        var collidingFields = FindCollidingFields(model, fields, diagnostics);
        if (collidingFields.Count > 0)
        {
            fields = fields.Where(field => !collidingFields.Contains(field)).ToArray();
        }
        var enumParserMembers = new List<(EnumMemberModel Member, string Token)>();
        var fieldFormatLengths = new List<(string Name, int Length)>();
        var longestCharLength = 0;
        var arrayType = $"{model.GeneratedName}Array";
        var metadataType = $"{model.GeneratedName}Extensions";
        var valuesInitializer = string.Join(", ", arrayMembers.Select(member => $"{EnumTypeAlias}.{member.Name}"));
        var createTargetConditionalValues = !model.NoInlineArray && arrayMembers.Length > 0;
        var usesByteSpan = model.CreateByteParsing || model.CreateIsValidPrefix || model.MappingSettings.Any(static settings => settings.IncludeUtf8Value);
        var enumCref = $"<see cref=\"{model.FullyQualifiedName}\"/>";
        var lengthDoc = "The number of distinct declared values, including the Unknown member (members sharing a numeric value count once).";
        var longestCharLengthDoc = model.IsFlags
            ? "The length of the longest string <c>ToStringFancy()</c> can return (every flag set, with a one-character separator): a safe <c>TryFormat</c> buffer size."
            : "The length of the longest string <c>ToStringFancy()</c> can return: a safe <c>TryFormat</c> buffer size.";
        var firstNonUnknownOrdinalDoc = "The underlying numeric value of <c>FirstNonUnknown</c>.";
        var firstNonUnknownDoc = hasUnknown
            ? $"The lowest-valued member other than <c>{unknownMember!.Name}</c>."
            : "The lowest-valued member.";
        var writer = new CodeWriter(FancyEnumNewFile);
        writer.AppendLine($"using {EnumTypeAlias} = {model.FullyQualifiedName};");
        writer.AppendLine($"using {CharSpanAlias} = global::System.ReadOnlySpan<char>;");
        if (usesByteSpan)
        {
            writer.AppendLine($"using {ByteSpanAlias} = global::System.ReadOnlySpan<byte>;");
        }
        writer.AppendLine($"using {StringComparisonAlias} = global::System.StringComparison;");
        // AsSpan/SequenceEqual/Equals(span, StringComparison) are MemoryExtensions extension methods, so they need
        // it in scope; consumers can't be assumed to have `using System;` (e.g. ImplicitUsings off). `using static`
        // imports only that class's members, never System's type names, so it can't clash with the consumer's types.
        writer.AppendLine("using static global::System.MemoryExtensions;");
        if (createTargetConditionalValues)
        {
            writer.AppendLine("#if NET8_0_OR_GREATER");
            writer.AppendLine($"using {InlineArrayAlias} = global::System.Runtime.CompilerServices.InlineArrayAttribute;");
            writer.AppendLine("#endif");
        }
        if (model.Namespace.Length > 0)
        {
            writer.AppendLine($"namespace {model.Namespace};");
        }

        if (createTargetConditionalValues)
        {
            writer.AppendLine("#if NET8_0_OR_GREATER");
            writer.AppendLine($"/// <summary>A fixed-size inline buffer of {arrayMembers.Length} {enumCref} values, as returned by <c>Values</c>/<c>ListFlagMembers</c>: a value type, so no heap allocation. Index it, <c>foreach</c> over it, or convert it to a span.</summary>");
            writer.AppendLine($"[{InlineArrayAlias}({arrayMembers.Length})]");
            using (var arrayWriter = writer.StartBraced($"{model.Accessibility} struct {arrayType}"))
            {
                arrayWriter.AppendLine($"private {model.FullyQualifiedName} _element0;");
            }
            writer.AppendLine("#endif");
        }

        writer.AppendLine($"/// <summary>FancyEnum-generated extension members for {enumCref}.</summary>");
        var classMembers = new List<Action<CodeWriter.BracedWriter>>();
        using (var classWriter = writer.StartBraced($"{model.Accessibility} static class {model.GeneratedName}FancyEnumExtensions"))
        {
            if (model.CreateStaticReadonlyCollection && arrayMembers.Length > 0)
            {
                // Opted into a cached, stable collection with a genuine AsSpan (below) - lazily built on first
                // access rather than eagerly, so an enum nobody ever calls Values/AsSpan on pays nothing.
                if (createTargetConditionalValues)
                {
                    classWriter.AppendLine("#if NET8_0_OR_GREATER");
                    AppendLazyInlineValuesField(classWriter, model, arrayMembers, arrayType);
                    classWriter.AppendLine("#else");
                }
                classWriter.AppendLine($"private static readonly {model.FullyQualifiedName}[] s_values = [{valuesInitializer}];");
                if (createTargetConditionalValues)
                {
                    classWriter.AppendLine("#endif");
                }
            }
            // else (the default): no static field at all. On .NET 8+, Values is built fresh on every access
            // (see AppendFreshInlineValues below) - cheap, since it's a value-type copy, not a heap allocation.
            // On older targets there's no allocation-free alternative to offer, so Values/AsSpan simply aren't
            // generated unless CreateStaticReadonlyCollection opts in.

            using (var valueExtension = classWriter.StartBraced($"extension({model.FullyQualifiedName} value)"))
            {
                AppendDoc(valueExtension, $"This value as its underlying <c>{model.UnderlyingType}</c>.");
                valueExtension.AppendLine($"public {model.UnderlyingType} AsUnderlying => ({model.UnderlyingType})value;");
                if (model.IsFlags)
                {
                    AppendDoc(valueExtension,
                        "Whether every bit of <paramref name=\"flag\"/> is set on this value: a non-boxing equivalent of <c>Enum.HasFlag</c>.",
                        "<see langword=\"true\"/> if <c>(value &amp; flag) == flag</c>.",
                        ("flag", "The flag (or combination of flags) to test for."));
                    valueExtension.AppendLine($"public bool HasFlagFancy({model.FullyQualifiedName} flag) => (({model.UnderlyingType})value & ({model.UnderlyingType})flag) == ({model.UnderlyingType})flag;");
                    if (createTargetConditionalValues)
                    {
                        valueExtension.AppendLine("#if NET8_0_OR_GREATER");
                        AppendListFlagMembers(valueExtension, model, arrayMembers, arrayType);
                        valueExtension.AppendLine("#endif");
                    }
                }
                longestCharLength = AppendDefaultStringProperties(valueExtension, model, members, diagnostics, enumParserMembers);
                if (hasUnknown)
                {
                    AppendDoc(valueExtension, $"Whether this value is <c>{unknownMember!.Name}</c> or isn't any declared member.");
                    if (nonUnknownMembers.Length == 0)
                    {
                        valueExtension.AppendLine("public bool IsUnknown => true;");
                    }
                    else if (nonUnknownMembersAreContiguous)
                    {
                        valueExtension.AppendLine($"public bool IsUnknown => ({model.UnderlyingType})value < ({model.UnderlyingType}){nonUnknownMembers[0].NumericValue.ToString(CultureInfo.InvariantCulture)} || ({model.UnderlyingType})value > ({model.UnderlyingType}){nonUnknownMembers[^1].NumericValue.ToString(CultureInfo.InvariantCulture)};");
                    }
                    else
                    {
                        valueExtension.AppendLine($"public bool IsUnknown => value is not ({string.Join(" or ", nonUnknownMembers.Select(member => $"{EnumTypeAlias}.{member.Name}"))});");
                    }
                    AppendDoc(valueExtension, "This value, or <c>FirstNonUnknown</c> if <c>IsUnknown</c>.");
                    valueExtension.AppendLine($"public {model.FullyQualifiedName} ValueOrDefaultIfUnknown => value.IsUnknown ? {firstNonUnknownExpression} : value;");
                    AppendDoc(valueExtension, "<c>ValueOrDefaultIfUnknown</c> as its underlying numeric value.");
                    valueExtension.AppendLine($"public {model.UnderlyingType} AsUnderlyingNonUnknown => value.IsUnknown ? ({model.UnderlyingType}){firstNonUnknownExpression} : ({model.UnderlyingType})value;");
                }
                foreach (var field in fields)
                {
                    AppendMappingProperty(valueExtension, model, members, field, diagnostics, fieldFormatLengths);
                }
            }

            using (var staticExtension = classWriter.StartBraced($"extension({model.FullyQualifiedName})"))
            {
                AppendDoc(staticExtension, lengthDoc);
                staticExtension.AppendLine($"public static int Length => {metadataType}.Length;");
                AppendDoc(staticExtension, longestCharLengthDoc);
                staticExtension.AppendLine($"public static int LongestCharLength => {metadataType}.LongestCharLength;");
                AppendDoc(staticExtension, firstNonUnknownOrdinalDoc);
                staticExtension.AppendLine($"public static {model.UnderlyingType} FirstNonUnknownOrdinal => {metadataType}.FirstNonUnknownOrdinal;");
                foreach (var fieldFormatLength in fieldFormatLengths)
                {
                    AppendDoc(staticExtension, FieldLongestCharLengthDoc(fieldFormatLength.Name));
                    staticExtension.AppendLine($"public static int {fieldFormatLength.Name}_LongestCharLength => {metadataType}.{fieldFormatLength.Name}_LongestCharLength;");
                }
                AppendDoc(staticExtension, firstNonUnknownDoc);
                staticExtension.AppendLine($"public static {model.FullyQualifiedName} FirstNonUnknown => {metadataType}.FirstNonUnknown;");

                var unknownDoc = hasUnknown ? $"<c>{unknownMember!.Name}</c>" : "<see langword=\"default\"/>";
                AppendDoc(staticExtension,
                    $"Converts a numeric value to the enum, returning {unknownDoc} if it isn't a declared member. Unlike a cast, the result is always a declared member.",
                    null,
                    ("underlying", "The numeric value to convert."));
                using (var fromUnderlyingWriter = staticExtension.StartBraced($"public static {model.FullyQualifiedName} FromUnderlying({model.UnderlyingType} underlying)"))
                {
                    if (members.Length == 0)
                    {
                        fromUnderlyingWriter.AppendLine($"return {unknownExpression};");
                    }
                    else if (membersAreContiguous)
                    {
                        fromUnderlyingWriter.AppendSimpleIf($"underlying >= ({model.UnderlyingType}){members[0].NumericValue.ToString(CultureInfo.InvariantCulture)} && underlying <= ({model.UnderlyingType}){members[^1].NumericValue.ToString(CultureInfo.InvariantCulture)}", $"return ({EnumTypeAlias})underlying;");
                        fromUnderlyingWriter.AppendLine($"return {unknownExpression};");
                    }
                    else
                    {
                        using var switchWriter = fromUnderlyingWriter.StartSwitchValue("return underlying switch");
                        foreach (var member in members)
                        {
                            switchWriter.AddBranch($"({model.UnderlyingType}){member.NumericValue.ToString(CultureInfo.InvariantCulture)}", $"{EnumTypeAlias}.{member.Name}");
                        }
                        switchWriter.AddDefaultArm(unknownExpression);
                    }
                }


                AppendDoc(staticExtension,
                    "Like <c>FromUnderlying</c>, but returns <c>FirstNonUnknown</c> instead when the value isn't a declared member (or is the Unknown member).",
                    null,
                    ("underlying", "The numeric value to convert."));
                if (!hasUnknown && members.Length > 0 && members[0].NumericValue == 0)
                {
                    staticExtension.AppendLine($"public static {model.FullyQualifiedName} FromUnderlyingNonUnknown({model.UnderlyingType} underlying) => FromUnderlying(underlying);");
                }
                else
                {
                    using var fromUnderlyingNonUnknownWriter = staticExtension.StartBraced($"public static {model.FullyQualifiedName} FromUnderlyingNonUnknown({model.UnderlyingType} underlying)");
                    if (nonUnknownMembers.Length == 0)
                    {
                        fromUnderlyingNonUnknownWriter.AppendLine($"return {firstNonUnknownExpression};");
                    }
                    else if (nonUnknownMembersAreContiguous)
                    {
                        fromUnderlyingNonUnknownWriter.AppendSimpleIf($"underlying >= ({model.UnderlyingType}){nonUnknownMembers[0].NumericValue.ToString(CultureInfo.InvariantCulture)} && underlying <= ({model.UnderlyingType}){nonUnknownMembers[^1].NumericValue.ToString(CultureInfo.InvariantCulture)}", $"return ({EnumTypeAlias})underlying;");
                        fromUnderlyingNonUnknownWriter.AppendLine($"return {firstNonUnknownExpression};");
                    }
                    else
                    {
                        using var switchWriter = fromUnderlyingNonUnknownWriter.StartSwitchValue($"return underlying switch");
                        foreach (var member in nonUnknownMembers)
                        {
                            switchWriter.AddBranch($"({model.UnderlyingType}){member.NumericValue.ToString(CultureInfo.InvariantCulture)}", $"{EnumTypeAlias}.{member.Name}");
                        }
                        switchWriter.AddDefaultArm(firstNonUnknownExpression);
                    }
                }

                if (model.GenerateParseMethods)
                {
                    AppendParserOverloads(staticExtension, model, enumParserMembers, "TryParseFancy", "enum names/default values", "a member's name or its <c>ToStringFancy()</c> string", model.ParseCaseSensitive, diagnostics, classMembers, includeBytePrefixParser: model.CreateIsValidPrefix);
                    var parseMethodName = hasUnknown ? "ParseOrUnknown" : "ParseOrDefault";
                    var parseFallback = hasUnknown ? unknownExpression : firstNonUnknownExpression;
                    var parseFallbackDoc = hasUnknown ? $"<c>{unknownMember!.Name}</c>" : "<c>FirstNonUnknown</c>";
                    foreach (var (spanType, encoding) in model.CreateByteParsing ? new[] { (CharSpanAlias, "text"), (ByteSpanAlias, "UTF-8 bytes") } : new[] { (CharSpanAlias, "text") })
                    {
                        var parseSummary = $"<c>TryParseFancy</c>, returning {parseFallbackDoc} instead of failing.";
                        AppendDoc(staticExtension, parseSummary, null, ("input", $"The {encoding} to parse."));
                        staticExtension.AppendLine($"public static {model.FullyQualifiedName} {parseMethodName}({spanType} input) => {EnumTypeAlias}.TryParseFancy(input, out var result) ? result : {parseFallback};");
                        AppendDoc(staticExtension, parseSummary, null, ("input", $"The {encoding} to parse."), ("ignoreCase", "Whether to ignore ASCII case."));
                        staticExtension.AppendLine($"public static {model.FullyQualifiedName} {parseMethodName}({spanType} input, bool ignoreCase) => {EnumTypeAlias}.TryParseFancy(input, ignoreCase, out var result) ? result : {parseFallback};");
                    }
                }
                if (model.CreateStaticReadonlyCollection && arrayMembers.Length > 0)
                {
                    if (createTargetConditionalValues)
                    {
                        staticExtension.AppendLine("#if NET8_0_OR_GREATER");
                        AppendCachedInlineValuesAndSpan(staticExtension, model, arrayType, arrayMembers.Length);
                        staticExtension.AppendLine("#else");
                    }
                    // s_values is a plain array here. Values is typed as IReadOnlyList<T> rather than T[] so
                    // callers can't reach in and mutate the shared static backing storage (arrays satisfy
                    // IReadOnlyList<T> natively - no wrapper object, so this costs nothing extra). AsSpan is
                    // still the implicit array-to-span conversion straight off the field itself.
                    AppendDoc(staticExtension, $"{ValuesDocPrefix}, backed by a shared static array. Read-only: the array itself is never exposed.");
                    staticExtension.AppendLine($"public static global::System.Collections.Generic.IReadOnlyList<{model.FullyQualifiedName}> Values => s_values;");
                    AppendDoc(staticExtension, AsSpanDoc);
                    staticExtension.AppendLine($"public static global::System.ReadOnlySpan<{model.FullyQualifiedName}> AsSpan => s_values;");
                    if (createTargetConditionalValues)
                    {
                        staticExtension.AppendLine("#endif");
                    }
                }
                else if (createTargetConditionalValues)
                {
                    staticExtension.AppendLine("#if NET8_0_OR_GREATER");
                    AppendFreshInlineValues(staticExtension, model, arrayMembers, arrayType);
                    staticExtension.AppendLine("#endif");
                }

                if (model.GenerateParseMethods)
                {
                    foreach (var settings in model.MappingSettings.Where(settings => settings.ParseFrom && !collidingFields.Contains(settings.FieldName)))
                    {
                        AppendParsers(staticExtension, model, members, settings, diagnostics, classMembers);
                    }
                }
            }

            // Private helpers the parsers above delegate to. They live at class level, outside the extension blocks.
            foreach (var classMember in classMembers)
            {
                classMember(classWriter);
            }
        }

        writer.AppendLine($"/// <summary>Constants describing {enumCref}, usable where a compile-time constant is required (<c>const</c> fields, attribute arguments, <c>stackalloc</c> sizes). Also exposed as static extension properties on the enum itself.</summary>");
        using (var metadataWriter = writer.StartBraced($"{model.Accessibility} static partial class {metadataType}"))
        {
            AppendDoc(metadataWriter, lengthDoc);
            metadataWriter.AppendLine($"public const int Length = {members.Length};");
            AppendDoc(metadataWriter, longestCharLengthDoc);
            metadataWriter.AppendLine($"public const int LongestCharLength = {longestCharLength};");
            AppendDoc(metadataWriter, firstNonUnknownOrdinalDoc);
            metadataWriter.AppendLine($"public const {model.UnderlyingType} FirstNonUnknownOrdinal = ({model.UnderlyingType}){firstNonUnknownOrdinal.ToString(CultureInfo.InvariantCulture)};");
            foreach (var fieldFormatLength in fieldFormatLengths)
            {
                AppendDoc(metadataWriter, FieldLongestCharLengthDoc(fieldFormatLength.Name));
                metadataWriter.AppendLine($"public const int {fieldFormatLength.Name}_LongestCharLength = {fieldFormatLength.Length};");
            }
            AppendDoc(metadataWriter, firstNonUnknownDoc);
            // A const, not a static readonly: enum values are compile-time constants, so this keeps the default output free
            // of static state (no field, no type initialization) and lets consumers use it in constant contexts.
            metadataWriter.AppendLine($"public const {model.FullyQualifiedName} FirstNonUnknown = {firstNonUnknownExpression};");
        }

        return new GenResult
        {
            FileName = $"{model.HintName}.FancyEnum{(model.UseGeneratedFileSuffix ? ".g" : "")}.cs",
            SourceCode = writer.AsString(),
            Diagnostics = diagnostics.ToEquatableArray()
        };
    }

    /// <summary>
    /// Writes an XML doc comment for a generated member. Every public generated member gets one, so a consumer
    /// building with GenerateDocumentationFile + TreatWarningsAsErrors doesn't fail on CS1591 in our output. When
    /// any parameter is documented, all of them must be (CS1573), so callers pass every parameter or none.
    /// </summary>
    private static void AppendDoc(CodeWriter.BracedWriter writer, string summary, string? returns = null, params (string Name, string Text)[] parameters)
    {
        writer.AppendLine($"/// <summary>{summary}</summary>");
        foreach (var (name, text) in parameters)
        {
            writer.AppendLine($"/// <param name=\"{name}\">{text}</param>");
        }
        if (returns is not null)
        {
            writer.AppendLine($"/// <returns>{returns}</returns>");
        }
    }

    /// <summary>
    /// Member names a mapped field may not generate. Reserved unconditionally, not only when the matching option is on,
    /// so turning an option on later can never break an existing field.
    /// </summary>
    private static readonly Dictionary<string, string> ReservedMemberNames = new(StringComparer.Ordinal)
    {
        ["AsUnderlying"] = "FancyEnum", ["HasFlagFancy"] = "FancyEnum", ["ListFlagMembers"] = "FancyEnum",
        ["ToStringFancy"] = "FancyEnum", ["DirectToStringFancy"] = "FancyEnum", ["TryFormat"] = "FancyEnum",
        ["IsUnknown"] = "FancyEnum", ["ValueOrDefaultIfUnknown"] = "FancyEnum", ["AsUnderlyingNonUnknown"] = "FancyEnum",
        ["Length"] = "FancyEnum", ["LongestCharLength"] = "FancyEnum", ["FirstNonUnknownOrdinal"] = "FancyEnum",
        ["FirstNonUnknown"] = "FancyEnum", ["FromUnderlying"] = "FancyEnum", ["FromUnderlyingNonUnknown"] = "FancyEnum",
        ["TryParseFancy"] = "FancyEnum", ["IsValidPrefixFancy"] = "FancyEnum", ["ParseOrUnknown"] = "FancyEnum",
        ["ParseOrDefault"] = "FancyEnum", ["Values"] = "FancyEnum", ["AsSpan"] = "FancyEnum",
        ["s_values"] = "FancyEnum", ["s_valuesInitialized"] = "FancyEnum",
        // Real instance members of every enum: an extension member with one of these names could never be called.
        ["ToString"] = "System.Enum", ["Equals"] = "System.Enum", ["GetHashCode"] = "System.Enum", ["GetType"] = "System.Enum",
        ["HasFlag"] = "System.Enum", ["CompareTo"] = "System.Enum", ["GetTypeCode"] = "System.Enum",
    };

    /// <summary>
    /// Reports (HENUM014) and returns the fields that would generate a member clashing with a reserved name or with a
    /// member another field generates - either would otherwise be a compile error inside the generated file.
    /// </summary>
    private static HashSet<string> FindCollidingFields(EnumModel model, string[] fields, List<DiagnosticInfo> diagnostics)
    {
        var colliding = new HashSet<string>(StringComparer.Ordinal);
        var generatedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var settings = model.MappingSettings.FirstOrDefault(setting => setting.FieldName == field);
            var propertyName = field.ToSafeCSharpIdentifier();
            var formatName = field.ToPascalCaseCSharpIdentifier();
            var names = new List<string> { propertyName };
            if (settings?.IncludeUtf8Value == true)
            {
                names.Add($"{propertyName}Bytes");
            }
            if (settings?.CreateTryFormat == true)
            {
                names.Add($"TryFormat_{formatName}");
                names.Add($"{formatName}_LongestCharLength");
            }
            if (settings?.ParseFrom == true)
            {
                names.Add($"TryParseFrom_{propertyName}");
            }

            foreach (var name in names)
            {
                string? reason = null;
                if (ReservedMemberNames.TryGetValue(name, out var reservedBy))
                {
                    reason = $"{reservedBy} already defines";
                }
                else if (generatedBy.TryGetValue(name, out var otherField))
                {
                    reason = $"field '{otherField}' also generates";
                }
                else
                {
                    generatedBy[name] = field;
                    continue;
                }
                var location = model.Members.FirstOrDefault(member => member.Mappings.Any(mapping => mapping.FieldName == field))?.Location ?? settings?.Location;
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.ReservedFieldName, location, model.FullyQualifiedName, field, name, reason));
                colliding.Add(field);
                break;
            }
        }
        return colliding;
    }

    private const string ValuesDocPrefix = "Every declared member except the Unknown member and any marked <c>ExcludeFromValues</c>, in numeric order";
    private const string AsSpanDoc = "The same members as <c>Values</c>, as a read-only span over the cached static storage: no copy, no allocation.";

    private static string FieldLongestCharLengthDoc(string formatName) =>
        $"The length of the longest string <c>TryFormat_{formatName}</c> can write: a safe buffer size for it.";

    /// <summary>Escapes arbitrary text (e.g. a user-supplied field name) for inclusion in a generated XML doc comment.</summary>
    /// <remarks>Field names are nearly always plain identifiers, so the common case is one scan and no allocation.</remarks>
    private static string XmlDoc(string text)
    {
        var index = text.IndexOfAny(XmlDocSpecialCharacters);
        if (index < 0)
        {
            return text;
        }
        var builder = new StringBuilder(text.Length + 8).Append(text, 0, index);
        for (; index < text.Length; index++)
        {
            var character = text[index];
            _ = character switch
            {
                '&' => builder.Append("&amp;"),
                '<' => builder.Append("&lt;"),
                '>' => builder.Append("&gt;"),
                _ => builder.Append(character)
            };
        }
        return builder.ToString();
    }

    private static readonly char[] XmlDocSpecialCharacters = ['&', '<', '>'];

    private static void AppendListFlagMembers(CodeWriter.BracedWriter writer, EnumModel model, EnumMemberModel[] arrayMembers, string arrayType)
    {
        var flags = arrayMembers.Where(member => IsSingleBit(member.NumericValue, model.UnderlyingType)).OrderBy(static member => member.NumericValue < 0).ThenBy(static member => member.NumericValue);
        AppendDoc(writer,
            "Lists the distinct single-bit flags set on this value, in bit order, without allocating. Zero, composite members, members excluded from <c>Values</c>, and undefined bits are omitted, so this is not a validity check.",
            null,
            ("values", "Receives the set flags; only the first <paramref name=\"length\"/> elements are meaningful."),
            ("length", "Receives the number of flags written to <paramref name=\"values\"/>."));
        using var methodWriter = writer.StartBraced($"public void ListFlagMembers(out {arrayType} values, out int length)");
        methodWriter.AppendLine("values = default;");
        methodWriter.AppendLine("length = 0;");
        foreach (var flag in flags)
        {
            using var flagWriter = methodWriter.StartBraced($"if ((value & {EnumTypeAlias}.{flag.Name}) != 0)");
            flagWriter.AppendLine($"values[length++] = {EnumTypeAlias}.{flag.Name};");
        }
    }

    private static bool AreContiguous(EnumMemberModel[] members) => members.Length > 0 && members.Zip(members.Skip(1), static (left, right) => right.NumericValue - left.NumericValue).All(static difference => difference == 1);

    private static int AppendDefaultStringProperties(CodeWriter.BracedWriter writer, EnumModel model, EnumMemberModel[] members, List<DiagnosticInfo> diagnostics, List<(EnumMemberModel Member, string Token)> enumParserMembers)
    {
        var values = new Dictionary<EnumMemberModel, (string Expression, string Text)>();
        foreach (var member in members)
        {
            enumParserMembers.Add((member, member.Name));
            var customValue = member.Mappings.FirstOrDefault(mapping => mapping.FieldName == model.DefaultToStringCustomField && mapping.ReturnType == "string")?.StringValue;
            var memberNameExpression = $"nameof({EnumTypeAlias}.{member.Name})";
            // DescriptionAttribute/DisplayAttribute/EnumMemberAttribute/JsonStringEnumMemberNameAttribute all
            // follow the same shape: read an optional well-known-attribute-derived string, falling back to the
            // member's own name (via nameof, so a later rename keeps the fallback in sync) when absent.
            var wellKnownAttributeText = model.DefaultToStringBehavior switch
            {
                FancyEnumDefaultToStringBehavior.DescriptionAttribute => member.Description,
                FancyEnumDefaultToStringBehavior.DisplayAttribute => member.DisplayName,
                FancyEnumDefaultToStringBehavior.EnumMemberAttribute => member.EnumMemberValue,
                FancyEnumDefaultToStringBehavior.JsonStringEnumMemberNameAttribute => member.JsonStringEnumMemberName,
                _ => null
            };
            var isWellKnownAttributeBehavior = model.DefaultToStringBehavior is FancyEnumDefaultToStringBehavior.DescriptionAttribute
                or FancyEnumDefaultToStringBehavior.DisplayAttribute
                or FancyEnumDefaultToStringBehavior.EnumMemberAttribute
                or FancyEnumDefaultToStringBehavior.JsonStringEnumMemberNameAttribute;
            string? text = model.DefaultToStringBehavior switch
            {
                FancyEnumDefaultToStringBehavior.NameOf => member.Name,
                FancyEnumDefaultToStringBehavior.NameOfLower => member.Name.ToLowerInvariant(),
                FancyEnumDefaultToStringBehavior.NameOfUpper => member.Name.ToUpperInvariant(),
                FancyEnumDefaultToStringBehavior.CustomFieldRequired or FancyEnumDefaultToStringBehavior.CustomFieldFallback => customValue,
                _ when isWellKnownAttributeBehavior => wellKnownAttributeText ?? member.Name,
                _ => member.Name
            };
            if (text is null && model.DefaultToStringBehavior == FancyEnumDefaultToStringBehavior.CustomFieldRequired)
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.MissingCustomStringMapping, member.Location, model.FullyQualifiedName, member.Name, model.DefaultToStringCustomField ?? ""));
                continue;
            }
            var useNameOf = model.DefaultToStringBehavior == FancyEnumDefaultToStringBehavior.NameOf
                || model.DefaultToStringBehavior == FancyEnumDefaultToStringBehavior.CustomFieldFallback && text is null
                || isWellKnownAttributeBehavior && wellKnownAttributeText is null;
            values[member] = (useNameOf ? memberNameExpression : (text ?? member.Name).ToCSharpStringLiteral(), text ?? member.Name);
            enumParserMembers.Add((member, text ?? member.Name));
        }

        if (!model.IsFlags)
        {
            AppendDoc(writer,
                $"This member's string form ({DescribeToStringBehavior(model)}), with no allocation or reflection.",
                "The member's string, or an empty string if this value isn't a declared member.");
        }
        using (var switchWriter = writer.StartSwitchValue(model.IsFlags ? "private string? DirectToStringFancy() => value switch" : "public string ToStringFancy() => value switch"))
        {
            foreach (var group in values.GroupBy(static value => value.Value.Expression, StringComparer.Ordinal))
            {
                switchWriter.AddBranch(string.Join(" or ", group.Select(value => $"{EnumTypeAlias}.{value.Key.Name}")), group.Key);
            }
            switchWriter.AddDefaultArm(model.IsFlags ? "null" : "string.Empty");
        }

        var longestCharLength = values.Count == 0 ? 0 : values.Max(static value => value.Value.Text.Length);
        if (model.IsFlags)
        {
            longestCharLength = Math.Max(longestCharLength, AppendFlagsToString(writer, model, values));
        }

        if (model.CreateTryFormat)
        {
            AppendDoc(writer,
                "Writes <c>ToStringFancy()</c> into <paramref name=\"destination\"/>. Size the buffer with <c>LongestCharLength</c>.",
                "<see langword=\"true\"/> if the string fit; otherwise <see langword=\"false\"/>, with nothing written.",
                ("destination", "The buffer to write into."),
                ("charsWritten", "Receives the number of characters written, or 0 on failure."));
            using var methodWriter = writer.StartBraced("public bool TryFormat(global::System.Span<char> destination, out int charsWritten)");
            methodWriter.AppendLine("var formatted = value.ToStringFancy().AsSpan();");
            using (var failureWriter = methodWriter.StartBraced("if (!formatted.TryCopyTo(destination))"))
            {
                failureWriter.AppendLine("charsWritten = 0;");
                failureWriter.AppendLine("return false;");
            }
            methodWriter.AppendLine("charsWritten = formatted.Length;");
            methodWriter.AppendLine("return true;");
        }

        return longestCharLength;
    }

    private static string DescribeToStringBehavior(EnumModel model) => model.DefaultToStringBehavior switch
    {
        FancyEnumDefaultToStringBehavior.NameOf => "the member's name",
        FancyEnumDefaultToStringBehavior.NameOfLower => "the member's name, lower-cased",
        FancyEnumDefaultToStringBehavior.NameOfUpper => "the member's name, upper-cased",
        FancyEnumDefaultToStringBehavior.CustomFieldRequired => $"the member's <c>{XmlDoc(model.DefaultToStringCustomField ?? "")}</c> mapping",
        FancyEnumDefaultToStringBehavior.CustomFieldFallback => $"the member's <c>{XmlDoc(model.DefaultToStringCustomField ?? "")}</c> mapping, else its name",
        FancyEnumDefaultToStringBehavior.DescriptionAttribute => "the member's <c>[Description]</c>, else its name",
        FancyEnumDefaultToStringBehavior.DisplayAttribute => "the member's <c>[Display(Name = ...)]</c>, else its name",
        FancyEnumDefaultToStringBehavior.EnumMemberAttribute => "the member's <c>[EnumMember(Value = ...)]</c>, else its name",
        FancyEnumDefaultToStringBehavior.JsonStringEnumMemberNameAttribute => "the member's <c>[JsonStringEnumMemberName]</c>, else its name",
        _ => "the member's name"
    };

    private static int AppendFlagsToString(CodeWriter.BracedWriter writer, EnumModel model, Dictionary<EnumMemberModel, (string Expression, string Text)> values)
    {
        var flags = values.Keys.Where(member => IsSingleBit(member.NumericValue, model.UnderlyingType)).OrderBy(member => member.NumericValue).ToArray();
        AppendDoc(writer,
            $"This value's string form ({DescribeToStringBehavior(model)}). A declared member, including a declared composite, returns its own string without allocating; any other combination joins the strings of its set flags in bit order, allocating only that result.",
            "The string, or an empty string if this value has bits that aren't any declared flag.",
            ("separator", "The character placed between flag strings in a combination."));
        using var methodWriter = writer.StartBraced("public string ToStringFancy(char separator = '|')");
        methodWriter.AppendLine("var direct = value.DirectToStringFancy();");
        using (var directWriter = methodWriter.StartBraced("if (direct is not null)"))
        {
            directWriter.AppendLine("return direct;");
        }

        if (flags.Length == 0)
        {
            methodWriter.AppendLine("return string.Empty;");
            return 0;
        }

        var knownFlags = string.Join(" | ", flags.Select(member => $"{EnumTypeAlias}.{member.Name}"));
        using (var unknownWriter = methodWriter.StartBraced($"if ((value & ~({knownFlags})) != 0)"))
        {
            unknownWriter.AppendLine("return string.Empty;");
        }

        // Reserve one separator per matched name, minus the leading separator.
        methodWriter.AppendLine("var length = -1;");
        foreach (var member in flags)
        {
            using var flagWriter = methodWriter.StartBraced($"if ((value & {EnumTypeAlias}.{member.Name}) != 0)");
            flagWriter.AppendLine($"length += {values[member].Text.Length + 1};");
        }

        using (var emptyWriter = methodWriter.StartBraced("if (length < 0)"))
        {
            emptyWriter.AppendLine("return string.Empty;");
        }

        methodWriter.AppendLine("#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER");
        using (var createWriter = methodWriter.StartBraced("return string.Create(length, (Flags: value, Separator: separator), static (destination, state) =>", new_line: false))
        {
            AppendFlagsCharacters(createWriter, flags, values, "state.Flags", "state.Separator", "destination");
        }
        methodWriter.AppendLine(");");

        methodWriter.AppendLine("#else");
        methodWriter.AppendLine("global::System.Span<char> formatted = length <= 1024 ? stackalloc char[length] : new char[length];");
        AppendFlagsCharacters(methodWriter, flags, values, "value", "separator", "formatted");
        methodWriter.AppendLine("return formatted.ToString();");
        methodWriter.AppendLine("#endif");
        return flags.Sum(member => values[member].Text.Length) + flags.Length - 1;
    }

    private static void AppendFlagsCharacters(CodeWriter.BracedWriter writer, EnumMemberModel[] flags, Dictionary<EnumMemberModel, (string Expression, string Text)> values, string value, string separator, string destination)
    {
        writer.AppendLine("var written = 0;");
        writer.AppendLine("var first = true;");
        foreach (var member in flags)
        {
            using var flagWriter = writer.StartBraced($"if (({value} & {EnumTypeAlias}.{member.Name}) != 0)");
            using (var separatorWriter = flagWriter.StartBraced("if (!first)"))
            {
                separatorWriter.AppendLine($"{destination}[written++] = {separator};");
            }
            // Track the first name separately from its length: custom labels may be empty.
            flagWriter.AppendLine("first = false;");
            // Slice rather than a range (destination[written..]): System.Range doesn't exist on netstandard2.0/net4x.
            flagWriter.AppendLine($"{values[member].Expression}.AsSpan().CopyTo({destination}.Slice(written));");
            flagWriter.AppendLine($"written += {values[member].Text.Length};");
        }
    }

    private static void AppendMappingSwitch(CodeWriter.BracedWriter writer, EnumModel model, string returnType, string propertyName, Dictionary<EnumMemberModel, string> mappings, string fallback)
    {
        // Only merge identical emitted expressions; computed mappings must retain their behavior.
        var groups = mappings.Where(mapping => !string.Equals(mapping.Value, fallback, StringComparison.Ordinal))
            .GroupBy(static mapping => mapping.Value, StringComparer.Ordinal)
            .Select(group => new { Expression = group.Key, Runs = GetConsecutiveMappingRuns(group.Select(static mapping => mapping.Key)).ToArray() }).ToArray();
        if (groups.Length == 0)
        {
            writer.AppendLine($"public {returnType} {propertyName} => {fallback};");
            return;
        }

        // Relational patterns require a numeric input, not an enum. Preserve its signedness and width.
        var useRanges = groups.Any(static group => group.Runs.Any(static run => run.Count >= 3));
        var input = useRanges ? $"(({model.UnderlyingType})value)" : "value";
        string Constant(EnumMemberModel member) => useRanges ? $"({model.UnderlyingType}){EnumTypeAlias}.{member.Name}" : $"{EnumTypeAlias}.{member.Name}";
        using var switchWriter = writer.StartSwitchValue($"public {returnType} {propertyName} => {input} switch");
        foreach (var group in groups)
        {
            var patterns = new List<string>();
            foreach (var run in group.Runs)
            {
                if (run.Count >= 3)
                {
                    patterns.Add($">= {Constant(run.First)} and <= {Constant(run.Last)}");
                }
                else
                {
                    patterns.Add(Constant(run.First));
                    if (run.Count == 2)
                    {
                        patterns.Add(Constant(run.Last));
                    }
                }
            }
            switchWriter.AddBranch(string.Join(" or ", patterns), group.Expression);
        }
        switchWriter.AddDefaultArm(fallback);
    }

    private static IEnumerable<(EnumMemberModel First, EnumMemberModel Last, int Count)> GetConsecutiveMappingRuns(IEnumerable<EnumMemberModel> members)
    {
        var sorted = members.OrderBy(static member => member.NumericValue).ToArray();
        for (var start = 0; start < sorted.Length;)
        {
            var end = start;
            // NumericValue is decimal, so adjacency checks also work at long/ulong boundaries.
            while (end + 1 < sorted.Length && sorted[end + 1].NumericValue - sorted[end].NumericValue == 1)
            {
                end++;
            }
            yield return (sorted[start], sorted[end], end - start + 1);
            start = end + 1;
        }
    }

    private static void AppendMappingProperty(CodeWriter.BracedWriter writer, EnumModel model, EnumMemberModel[] members, string fieldName, List<DiagnosticInfo> diagnostics, List<(string Name, int Length)> fieldFormatLengths)
    {
        var settings = model.MappingSettings.FirstOrDefault(setting => setting.FieldName == fieldName) ?? new MappingSettingsModel { FieldName = fieldName };
        var explicitMappings = members.Select(member => (Member: member, Mapping: member.Mappings.FirstOrDefault(mapping => mapping.FieldName == fieldName))).ToArray();
        var declaredReturnTypes = explicitMappings.Where(static item => item.Mapping is not null).Select(static item => item.Mapping!.ReturnType).ToList();
        if (settings.ReturnType is not null)
        {
            declaredReturnTypes.Add(settings.ReturnType);
        }
        else if (settings.NotDefined != FancyEnumMemberFallbackOption.Skip || settings.NotMatchedExpression is not null)
        {
            declaredReturnTypes.Add("string");
        }
        var returnTypes = declaredReturnTypes.Distinct(StringComparer.Ordinal).ToArray();
        if (returnTypes.Length > 1)
        {
            diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.InvalidMapping, null, model.FullyQualifiedName, fieldName, "member mappings use different return types"));
            return;
        }

        var returnType = returnTypes.FirstOrDefault() ?? "string";
        // A non-string reference-typed field (Type, object, ...) with no NotMatched/Throw can only fall back to null - and
        // the fallback is always reachable, via any undeclared value - so the property has to say it's nullable.
        var isNonStringReferenceType = settings.IsNonStringReferenceType || explicitMappings.Any(static item => item.Mapping?.IsNonStringReferenceType == true);
        var fallsBackToNull = !settings.ThrowOnNotMatched && settings.NotMatchedExpression is null && (settings.ReturnNullOnNotMatched || isNonStringReferenceType);
        var propertyReturnType = fallsBackToNull && !returnType.EndsWith("?", StringComparison.Ordinal) ? $"{returnType}?" : returnType;
        var mappings = new Dictionary<EnumMemberModel, string>();
        foreach (var (member, mapping) in explicitMappings)
        {
            if (mapping is not null)
            {
                mappings[member] = mapping.Expression;
                continue;
            }
            if (settings.NotDefinedExpression is not null)
            {
                mappings[member] = settings.NotDefinedExpression;
            }
            else if (returnType == "string" && settings.NotDefined != FancyEnumMemberFallbackOption.Skip)
            {
                mappings[member] = settings.NotDefined switch
                {
                    FancyEnumMemberFallbackOption.NameOf => $"nameof({EnumTypeAlias}.{member.Name})",
                    FancyEnumMemberFallbackOption.NameOfLower => member.Name.ToLowerInvariant().ToCSharpStringLiteral(),
                    FancyEnumMemberFallbackOption.NameOfUpper => member.Name.ToUpperInvariant().ToCSharpStringLiteral(),
                    _ => "string.Empty"
                };
            }
            else if (returnType != "string" && settings.NotDefined != FancyEnumMemberFallbackOption.Skip)
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.InvalidMapping, member.Location, model.FullyQualifiedName, fieldName, "NotDefined name fallbacks require a string mapping"));
            }
        }

        // FormatConstant emits a null attribute argument as `default`, and only reference types can be null in an
        // attribute, so any `default` value means the property can return null and must say so (else CS8603 in
        // generated code for consumers with nullable enabled).
        if (!propertyReturnType.EndsWith("?", StringComparison.Ordinal) && mappings.Values.Any(static expression => expression == "default"))
        {
            propertyReturnType = $"{returnType}?";
        }
        var propertyName = fieldName.ToSafeCSharpIdentifier().EscapeCSharpIdentifier();
        var fallback = settings.ThrowOnNotMatched
            ? "throw new global::System.ArgumentOutOfRangeException(nameof(value), value, null)"
            : settings.NotMatchedExpression ?? (fallsBackToNull ? "null" : returnType == "string" ? "string.Empty" : "default");
        var fieldDoc = XmlDoc(fieldName);
        var fallbackDoc = settings.ThrowOnNotMatched
            ? "throws <see cref=\"global::System.ArgumentOutOfRangeException\"/>"
            : settings.NotMatchedExpression is not null
                ? settings.NotMatchedStringValue is { } notMatchedText ? $"returns <c>\"{XmlDoc(notMatchedText)}\"</c>" : "returns the configured <c>NotMatched</c> value"
                : fallsBackToNull ? "returns <see langword=\"null\"/>" : returnType == "string" ? "returns an empty string" : "returns <see langword=\"default\"/>";
        AppendDoc(writer, $"This member's <c>{fieldDoc}</c> value. For a member without one (or a value that isn't a declared member), {fallbackDoc}.");
        AppendMappingSwitch(writer, model, propertyReturnType, propertyName, mappings, fallback);

        if (settings.IncludeUtf8Value)
        {
            var utf8UnavailableReasons = new List<string>();
            if (returnType != "string")
            {
                utf8UnavailableReasons.Add("the mapping does not return string");
            }
            if (explicitMappings.Any(static item => item.Mapping is { ReturnType: "string", StringValue: null }))
            {
                utf8UnavailableReasons.Add("a member mapping is computed at runtime");
            }
            if (utf8UnavailableReasons.Count > 0)
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.Utf8ValueUnavailable, settings.Location, fieldName, string.Join(" and ", utf8UnavailableReasons)));
            }
            else
            {
                var utf8Mappings = new Dictionary<EnumMemberModel, string>();
                foreach (var (member, mapping) in explicitMappings)
                {
                    string? value = mapping?.StringValue;
                    if (mapping is null)
                    {
                        value = settings.NotDefinedExpression is not null
                            ? settings.NotDefinedStringValue
                            : settings.NotDefined switch
                            {
                                FancyEnumMemberFallbackOption.NameOf => member.Name,
                                FancyEnumMemberFallbackOption.NameOfLower => member.Name.ToLowerInvariant(),
                                FancyEnumMemberFallbackOption.NameOfUpper => member.Name.ToUpperInvariant(),
                                _ => null
                            };
                    }
                    if (value is not null)
                    {
                        utf8Mappings[member] = $"{value.ToCSharpStringLiteral()}u8";
                    }
                }
                var utf8PropertyName = $"{fieldName.ToSafeCSharpIdentifier()}Bytes".EscapeCSharpIdentifier();
                var utf8Fallback = settings.ThrowOnNotMatched
                    ? "throw new global::System.ArgumentOutOfRangeException(nameof(value), value, null)"
                    : settings.NotMatchedStringValue is { } notMatched ? $"{notMatched.ToCSharpStringLiteral()}u8" : "\"\"u8";
                AppendDoc(writer, $"This member's <c>{fieldDoc}</c> value as UTF-8 bytes, from a <c>u8</c> literal: no allocation or transcoding. Falls back the same way as <c>{propertyName}</c>, with an empty span in place of <see langword=\"null\"/>.");
                AppendMappingSwitch(writer, model, ByteSpanAlias, utf8PropertyName, utf8Mappings, utf8Fallback);
            }
        }

        if (!settings.CreateTryFormat)
        {
            return;
        }

        var unavailableReasons = new List<string>();
        if (returnType != "string")
        {
            unavailableReasons.Add("the mapping does not return string");
        }
        if (explicitMappings.Any(static item => item.Mapping?.HasStaticMemberSource == true))
        {
            unavailableReasons.Add("a member mapping defines StaticMethodSource");
        }
        if (unavailableReasons.Count > 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.TryFormatUnavailable, settings.Location, fieldName, string.Join(" and ", unavailableReasons)));
            return;
        }

        var knownValues = new List<string>();
        foreach (var (member, mapping) in explicitMappings)
        {
            if (mapping is not null)
            {
                knownValues.Add(mapping.StringValue ?? "");
            }
            else if (settings.NotDefinedExpression is not null)
            {
                knownValues.Add(settings.NotDefinedStringValue ?? "");
            }
            else if (settings.NotDefined != FancyEnumMemberFallbackOption.Skip)
            {
                knownValues.Add(settings.NotDefined switch
                {
                    FancyEnumMemberFallbackOption.NameOf => member.Name,
                    FancyEnumMemberFallbackOption.NameOfLower => member.Name.ToLowerInvariant(),
                    FancyEnumMemberFallbackOption.NameOfUpper => member.Name.ToUpperInvariant(),
                    _ => ""
                });
            }
            else if (!settings.ThrowOnNotMatched)
            {
                knownValues.Add(settings.NotMatchedStringValue ?? "");
            }
        }
        if (!settings.ThrowOnNotMatched)
        {
            knownValues.Add(settings.NotMatchedStringValue ?? "");
        }

        var formatName = fieldName.ToPascalCaseCSharpIdentifier();
        fieldFormatLengths.Add((formatName, knownValues.Count == 0 ? 0 : knownValues.Max(static value => value.Length)));
        AppendDoc(writer,
            $"Writes this member's <c>{fieldDoc}</c> value into <paramref name=\"destination\"/>. Size the buffer with <c>{formatName}_LongestCharLength</c>.",
            "<see langword=\"true\"/> if the value fit; otherwise <see langword=\"false\"/>, with nothing written.",
            ("destination", "The buffer to write into."),
            ("charsWritten", "Receives the number of characters written, or 0 on failure."));
        using var methodWriter = writer.StartBraced($"public bool TryFormat_{formatName}(global::System.Span<char> destination, out int charsWritten)");
        methodWriter.AppendLine($"var formatted = value.{propertyName}.AsSpan();");
        using (var failureWriter = methodWriter.StartBraced("if (!formatted.TryCopyTo(destination))"))
        {
            failureWriter.AppendLine("charsWritten = 0;");
            failureWriter.AppendLine("return false;");
        }
        methodWriter.AppendLine("charsWritten = formatted.Length;");
        methodWriter.AppendLine("return true;");
    }

    /// <summary>
    /// Declares the lazily-populated backing field for the .NET 8+ inline-array Values/AsSpan pair (opted into
    /// via CreateStaticReadonlyCollection), plus the ensure/factory methods that populate it on first access.
    /// Lazy rather than an eager field initializer, so an enum nobody ever calls Values/AsSpan on pays nothing.
    /// A stable field (rather than rebuilding the array on every access) is also what lets AsSpan create a
    /// genuine, zero-copy span over it - see AppendCachedInlineValuesAndSpan.
    /// </summary>
    private static void AppendLazyInlineValuesField(CodeWriter.BracedWriter writer, EnumModel model, EnumMemberModel[] members, string arrayType)
    {
        writer.AppendLine($"private static {arrayType} s_values;");
        writer.AppendLine("private static volatile bool s_valuesInitialized;");
        using (var methodWriter = writer.StartBraced($"private static void Ensure{model.GeneratedName}Values()"))
        {
            using (var ifWriter = methodWriter.StartBraced("if (!s_valuesInitialized)"))
            {
                ifWriter.AppendLine($"s_values = Create{model.GeneratedName}Values();");
                ifWriter.AppendLine("s_valuesInitialized = true;");
            }
        }
        using (var methodWriter = writer.StartBraced($"private static {arrayType} Create{model.GeneratedName}Values()"))
        {
            methodWriter.AppendLine($"{arrayType} values = default;");
            for (var index = 0; index < members.Length; index++)
            {
                methodWriter.AppendLine($"values[{index}] = {EnumTypeAlias}.{members[index].Name};");
            }
            methodWriter.AppendLine("return values;");
        }
    }

    /// <summary>Values/AsSpan for the CreateStaticReadonlyCollection + .NET 8+ case: both trigger the lazy ensure, then AsSpan reinterprets the now-populated static field's storage directly (never a by-value copy - see FancyEnumSourceGenerator.Emit.cs comments elsewhere on why that matters for correctness).</summary>
    private static void AppendCachedInlineValuesAndSpan(CodeWriter.BracedWriter writer, EnumModel model, string arrayType, int length)
    {
        AppendDoc(writer, $"{ValuesDocPrefix}, cached in a static field on first access. Returned by value, so the caller gets its own copy of the inline buffer.");
        using (var propertyWriter = writer.StartBraced($"public static {arrayType} Values"))
        {
            using (var getterWriter = propertyWriter.StartBraced("get"))
            {
                getterWriter.AppendLine($"Ensure{model.GeneratedName}Values();");
                getterWriter.AppendLine("return s_values;");
            }
        }
        AppendDoc(writer, AsSpanDoc);
        using (var propertyWriter = writer.StartBraced($"public static global::System.ReadOnlySpan<{model.FullyQualifiedName}> AsSpan"))
        {
            using (var getterWriter = propertyWriter.StartBraced("get"))
            {
                getterWriter.AppendLine($"Ensure{model.GeneratedName}Values();");
                getterWriter.AppendLine($"return global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref global::System.Runtime.CompilerServices.Unsafe.As<{arrayType}, {model.FullyQualifiedName}>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in s_values)), {length});");
            }
        }
    }

    /// <summary>Values for the default (CreateStaticReadonlyCollection = false) .NET 8+ case: built fresh on every access - no static field, so no AsSpan either (there is nothing stable to point a span at).</summary>
    private static void AppendFreshInlineValues(CodeWriter.BracedWriter writer, EnumModel model, EnumMemberModel[] members, string arrayType)
    {
        AppendDoc(writer, $"{ValuesDocPrefix}. Built fresh on each access as an inline value-type buffer, so there's no heap allocation and no static storage; set <c>CreateStaticReadonlyCollection</c> for a cached copy plus <c>AsSpan</c>.");
        using (var propertyWriter = writer.StartBraced($"public static {arrayType} Values"))
        {
            using (var getterWriter = propertyWriter.StartBraced("get"))
            {
                getterWriter.AppendLine($"{arrayType} values = default;");
                for (var index = 0; index < members.Length; index++)
                {
                    getterWriter.AppendLine($"values[{index}] = {EnumTypeAlias}.{members[index].Name};");
                }
                getterWriter.AppendLine("return values;");
            }
        }
    }

    private static void AppendParsers(CodeWriter.BracedWriter writer, EnumModel model, EnumMemberModel[] members, MappingSettingsModel settings, List<DiagnosticInfo> diagnostics, List<Action<CodeWriter.BracedWriter>> classMembers)
    {
        var fieldMappings = members.Select(member => (Member: member, Mapping: member.Mappings.FirstOrDefault(mapping => mapping.FieldName == settings.FieldName))).Where(static item => item.Mapping is not null).ToArray();
        var unavailableReasons = new List<string>();
        if (settings.ReturnType is not null && settings.ReturnType != "string" || fieldMappings.Any(static item => item.Mapping!.ReturnType != "string"))
        {
            unavailableReasons.Add("the mapping does not return string");
        }
        if (fieldMappings.Any(static item => item.Mapping!.HasStaticMemberSource))
        {
            unavailableReasons.Add("a member mapping defines StaticMethodSource");
        }
        var parserMembers = new List<(EnumMemberModel Member, string Token)>();
        foreach (var member in members)
        {
            var mapping = member.Mappings.FirstOrDefault(mapping => mapping.FieldName == settings.FieldName);
            if (mapping?.StringValue is { } mappedToken)
            {
                parserMembers.Add((member, mappedToken));
                continue;
            }
            if (mapping is not null)
            {
                continue;
            }
            var fallbackToken = settings.NotDefinedExpression is not null
                ? settings.NotDefinedStringValue
                : settings.NotDefined switch
                {
                    FancyEnumMemberFallbackOption.NameOf => member.Name,
                    FancyEnumMemberFallbackOption.NameOfLower => member.Name.ToLowerInvariant(),
                    FancyEnumMemberFallbackOption.NameOfUpper => member.Name.ToUpperInvariant(),
                    _ => null
                };
            if (fallbackToken is not null)
            {
                parserMembers.Add((member, fallbackToken));
            }
        }
        if (parserMembers.Count == 0)
        {
            unavailableReasons.Add("the mapping has no string values to parse");
        }
        if (unavailableReasons.Count > 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.ParseUnavailable, settings.Location, settings.FieldName, string.Join(" and ", unavailableReasons)));
            return;
        }

        AppendParserOverloads(writer, model, parserMembers, $"TryParseFrom_{settings.FieldName.ToSafeCSharpIdentifier()}", settings.FieldName, $"a member's <c>{XmlDoc(settings.FieldName)}</c> value", settings.ParseCaseSensitive ?? model.ParseCaseSensitive, diagnostics, classMembers);
    }

    private static void AppendParserOverloads(CodeWriter.BracedWriter writer, EnumModel model, IEnumerable<(EnumMemberModel Member, string Token)> parserMembers, string methodName, string parserSource, string acceptsDoc, bool caseSensitive, List<DiagnosticInfo> diagnostics, List<Action<CodeWriter.BracedWriter>> classMembers, bool includeBytePrefixParser = false)
    {
        static (EnumMemberModel Member, string Token) PreferNonUnknown(IEnumerable<(EnumMemberModel Member, string Token)> candidates) =>
            candidates.OrderBy(static item => IsUnknownMember(item.Member) ? 1 : 0).First();

        // Ambiguity is judged by the enum's default case sensitivity: that's what an unqualified parse call uses...
        parserMembers = parserMembers.ToList();
        foreach (var group in parserMembers.GroupBy(static item => item.Token, caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase))
        {
            if (group.Where(static item => !IsUnknownMember(item.Member)).Select(static item => item.Member.NumericValue).Distinct().Skip(1).Any())
            {
                var selected = PreferNonUnknown(group);
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.AmbiguousParserToken, selected.Member.Location, parserSource, selected.Token, selected.Member.Name));
            }
        }
        // ...but every exact spelling is kept, even when the default ignores case: the explicit ignoreCase: false
        // overload must still match each one exactly (e.g. a member Te whose wire name is "TE"). The ignore-case
        // lookups collapse spellings on their own.
        var uniqueMembers = parserMembers.GroupBy(static item => item.Token, StringComparer.Ordinal).Select(PreferNonUnknown).ToList();

        AppendParser(writer, model, uniqueMembers, methodName, acceptsDoc, caseSensitive, isBytes: false, classMembers);
        if (model.CreateByteParsing)
        {
            AppendParser(writer, model, uniqueMembers, methodName, acceptsDoc, caseSensitive, isBytes: true, classMembers);
        }
        // Opt-in (CreateIsValidPrefix) and independent of CreateByteParsing: it's for incremental/streaming decoders
        // (docs/is-valid-prefix.md), a niche most enums don't need.
        if (includeBytePrefixParser)
        {
            const string prefixSummary = "Whether <paramref name=\"prefix\"/> followed by <paramref name=\"next\"/> is the start of some member's parseable string (see <c>TryParseFancy</c>), for incremental parsing of UTF-8 input without first concatenating the pieces. Checks every token in turn, so its cost grows with the enum's size.";
            const string prefixReturns = "<see langword=\"true\"/> if at least one member's string starts with the combined bytes.";
            AppendDoc(writer, $"{prefixSummary} {(caseSensitive ? "Case-sensitive." : "Ignores ASCII case.")}", prefixReturns,
                ("prefix", "The bytes seen so far."),
                ("next", "The bytes that follow <paramref name=\"prefix\"/>."));
            writer.AppendLine($"public static bool IsValidPrefixFancy({ByteSpanAlias} prefix, {ByteSpanAlias} next) => {EnumTypeAlias}.IsValidPrefixFancy(prefix, next, {(!caseSensitive).ToString().ToLowerInvariant()});");
            AppendDoc(writer, prefixSummary, prefixReturns,
                ("prefix", "The bytes seen so far."),
                ("next", "The bytes that follow <paramref name=\"prefix\"/>."),
                ("ignoreCase", "Whether to ignore ASCII case."));
            using var prefixWriter = writer.StartBraced($"public static bool IsValidPrefixFancy({ByteSpanAlias} prefix, {ByteSpanAlias} next, bool ignoreCase)");
            foreach (var member in uniqueMembers.OrderBy(static member => member.Token, StringComparer.Ordinal))
            {
                prefixWriter.AppendSimpleIf($"{ParsingHelpersType}.AsciiStartsWithCombined({member.Token.ToCSharpStringLiteral()}u8, prefix, next, ignoreCase)", "return true;");
            }
            prefixWriter.AppendLine("return false;");
        }
    }

    private static void AppendParser(CodeWriter.BracedWriter writer, EnumModel model, List<(EnumMemberModel Member, string Token)> members, string methodName, string acceptsDoc, bool caseSensitive, bool isBytes, List<Action<CodeWriter.BracedWriter>> classMembers)
    {
        var spanType = isBytes ? ByteSpanAlias : CharSpanAlias;
        var summary = $"Parses {acceptsDoc}{(isBytes ? ", from UTF-8 bytes" : "")}. Exact matches only: no numeric strings, whitespace trimming, or comma-separated flags.";
        const string returns = "<see langword=\"true\"/> if <paramref name=\"input\"/> matched a member; otherwise <see langword=\"false\"/>.";
        var inputDoc = ("input", isBytes ? "The UTF-8 bytes to parse." : "The text to parse.");
        var resultDoc = ("result", "Receives the matched member, or <see langword=\"default\"/> if there was no match.");
        AppendDoc(writer, $"{summary} {(caseSensitive ? "Case-sensitive." : "Ignores case.")}", returns, inputDoc, resultDoc);
        writer.AppendLine($"public static bool {methodName}({spanType} input, out {model.FullyQualifiedName} result) => {EnumTypeAlias}.{methodName}(input, {(!caseSensitive).ToString().ToLowerInvariant()}, out result);");
        AppendDoc(writer, summary, returns, inputDoc, ("ignoreCase", "Whether to ignore case."), resultDoc);
        using var methodWriter = writer.StartBraced($"public static bool {methodName}({spanType} input, bool ignoreCase, out {model.FullyQualifiedName} result)");
        if (members.Count < DirectParserComparisonThreshold)
        {
            foreach (var member in members.OrderBy(static member => member.Token, StringComparer.Ordinal))
            {
                methodWriter.AppendSimpleIf(GetDirectParserCondition(member.Token, isBytes), $"result = {EnumTypeAlias}.{member.Member.Name}; return true;");
            }
        }
        else if (isBytes)
        {
            AppendByteSwitchParser(methodWriter, members);
        }
        else
        {
            AppendCharSwitchParser(methodWriter, model, members, methodName, classMembers);
        }
        methodWriter.AppendLine("result = default;");
        methodWriter.AppendLine("return false;");
    }

    private static string ParserMatch(EnumMemberModel member) => $"result = {EnumTypeAlias}.{member.Name}; return true;";

    /// <summary>
    /// The tokens an ignore-case parser must distinguish: one per case-insensitively distinct token (keyed by its
    /// upper-cased form), preferring a non-Unknown member, as the case-sensitive dedupe does. Without this, tokens
    /// differing only by case would become duplicate case labels.
    /// </summary>
    private static IEnumerable<(string Upper, EnumMemberModel Member)> DistinctIgnoringCase(IEnumerable<(EnumMemberModel Member, string Token)> members) =>
        members.GroupBy(static member => member.Token.ToUpperInvariant(), StringComparer.Ordinal)
            .Select(static group => (group.Key, group.OrderBy(static member => IsUnknownMember(member.Member) ? 1 : 0).First().Member))
            .OrderBy(static member => member.Key, StringComparer.Ordinal);

    /// <summary>
    /// Text parsing for enough tokens that comparing them one by one would be slow. The public method only switches on
    /// the input's length, then calls a small private helper per length, and each helper <c>switch</c>es over just that
    /// length's tokens as string constants. Roslyn lowers such a switch to a branch on one distinguishing character and
    /// then a single comparison, so a lookup costs about the same at any enum size.
    /// </summary>
    /// <remarks>
    /// Case-insensitive lookups upper-case the input into a stack buffer and use helpers over upper-cased constants.
    /// Per-character invariant upper-casing is exactly how <see cref="StringComparison.OrdinalIgnoreCase"/> compares, so
    /// this matches the direct-comparison path used for small enums.
    /// <para>
    /// The per-length split matters for large enums. One method switching over hundreds of string constants holds a
    /// span temporary per case; past what the JIT can track, it zero-initializes all of them on every call, which cost
    /// ~100 ns per lookup, hit or miss, on a 200-member enum.
    /// </para>
    /// </remarks>
    private static void AppendCharSwitchParser(CodeWriter.BracedWriter writer, EnumModel model, List<(EnumMemberModel Member, string Token)> members, string methodName, List<Action<CodeWriter.BracedWriter>> classMembers)
    {
        var caseSensitiveGroups = members.GroupBy(static member => member.Token.Length).OrderBy(static group => group.Key)
            .Select(static group => (Length: group.Key, Members: group.Select(static member => (member.Token, member.Member)).OrderBy(static member => member.Token, StringComparer.Ordinal).ToList()))
            .ToList();
        var ignoreCaseGroups = DistinctIgnoringCase(members).GroupBy(static member => member.Upper.Length).OrderBy(static group => group.Key)
            .Select(static group => (Length: group.Key, Members: group.Select(static member => (Token: member.Upper, member.Member)).ToList()))
            .ToList();
        string HelperName(bool ignoreCase, int length) => $"{methodName}__{(ignoreCase ? "IgnoreCase" : "Exact")}{length.ToString(CultureInfo.InvariantCulture)}";

        var caseSensitiveHelpers = new List<(int Length, List<(string Token, EnumMemberModel Member)> Members)>();
        var ignoreCaseHelpers = new List<(int Length, List<(string Token, EnumMemberModel Member)> Members)>();
        using (var caseSensitiveWriter = writer.StartBraced("if (!ignoreCase)"))
        {
            caseSensitiveHelpers = AppendLengthDispatch(caseSensitiveWriter, caseSensitiveGroups, length => HelperName(false, length), "input");
        }
        var longest = members.Max(static member => member.Token.Length);
        using (var ignoreCaseWriter = writer.StartBraced($"else if (input.Length <= {longest.ToString(CultureInfo.InvariantCulture)})"))
        {
            // Bounded by the longest token, so this is always small; a pathologically long token gets a heap buffer instead.
            ignoreCaseWriter.AppendLine($"global::System.Span<char> upper = {(longest <= 256 ? "stackalloc" : "new")} char[{longest.ToString(CultureInfo.InvariantCulture)}];");
            ignoreCaseWriter.AppendLine($"{ParsingHelpersType}.ToUpperInvariant(input, upper);");
            ignoreCaseWriter.AppendLine($"{CharSpanAlias} upperInput = upper.Slice(0, input.Length);");
            ignoreCaseHelpers = AppendLengthDispatch(ignoreCaseWriter, ignoreCaseGroups, length => HelperName(true, length), "upperInput");
        }

        classMembers.Add(classWriter =>
        {
            foreach (var (ignoreCase, groups) in new[] { (false, caseSensitiveHelpers), (true, ignoreCaseHelpers) })
            {
                foreach (var (length, groupMembers) in groups)
                {
                    using var helperWriter = classWriter.StartBraced($"private static bool {HelperName(ignoreCase, length)}({CharSpanAlias} input, out {model.FullyQualifiedName} result)");
                    using (var switchWriter = helperWriter.StartSwitchCases("switch (input)"))
                    {
                        foreach (var (token, member) in groupMembers)
                        {
                            switchWriter.AddCase(token.ToCSharpStringLiteral(), caseWriter => caseWriter.AppendLine(ParserMatch(member)), "");
                        }
                    }
                    helperWriter.AppendLine("result = default;");
                    helperWriter.AppendLine("return false;");
                }
            }
        });
    }

    /// <summary>How many direct comparisons one length dispatch may inline before the rest go to helpers; see <see cref="AppendLengthDispatch"/>.</summary>
    private const int InlineComparisonBudget = 32;

    /// <summary>
    /// <c>switch (input.Length)</c>. A length with only a few tokens is compared right there (a <c>switch</c> would add
    /// nothing but a method call); a larger one returns its helper's answer. Any other length falls through.
    /// </summary>
    /// <remarks>
    /// Each inline comparison adds a span temporary to the enclosing method, and too many of those is exactly what made
    /// one giant parse method slow (the JIT zero-initializes them all on every call). So inlining is capped at
    /// <see cref="InlineComparisonBudget"/> comparisons per dispatch; anything past that gets a helper regardless of size.
    /// </remarks>
    /// <returns>The groups that need a helper method.</returns>
    private static List<(int Length, List<(string Token, EnumMemberModel Member)> Members)> AppendLengthDispatch(
        CodeWriter.BracedWriter writer,
        List<(int Length, List<(string Token, EnumMemberModel Member)> Members)> groups,
        Func<int, string> helperName,
        string input)
    {
        var needHelpers = new List<(int Length, List<(string Token, EnumMemberModel Member)> Members)>();
        var inlined = 0;
        using var switchWriter = writer.StartSwitchCases("switch (input.Length)");
        foreach (var group in groups)
        {
            var caseValue = group.Length.ToString(CultureInfo.InvariantCulture);
            if (group.Members.Count <= DirectParserComparisonThreshold && inlined + group.Members.Count <= InlineComparisonBudget)
            {
                inlined += group.Members.Count;
                switchWriter.AddCase(caseValue, caseWriter =>
                {
                    foreach (var (token, member) in group.Members)
                    {
                        caseWriter.AppendSimpleIf($"{input}.SequenceEqual({token.ToCSharpStringLiteral()}.AsSpan())", ParserMatch(member));
                    }
                }, "break;");
                continue;
            }
            needHelpers.Add(group);
            switchWriter.AddCase(caseValue, caseWriter => caseWriter.AppendLine($"return {helperName(group.Length)}({input}, out result);"), "");
        }
        return needHelpers;
    }

    /// <summary>
    /// UTF-8 parsing for enough tokens that comparing them one by one would be slow. <c>u8</c> literals aren't constants,
    /// so the compiler can't lower a switch over them; instead, per input length, this switches on the input's first 8
    /// bytes packed into a <c>ulong</c> (which the compiler lowers to a binary search), then checks the next 8 bytes and
    /// any tail. Exact and case-insensitive matching get separate, pre-upper-cased constants.
    /// </summary>
    /// <remarks>
    /// The approach is inspired by StackExchange.Redis's AsciiHash (eng/StackExchange.Redis.Build/AsciiHash.md). A token
    /// containing non-ASCII characters can't be packed, so it's compared directly - and case-sensitively, as before.
    /// </remarks>
    private static void AppendByteSwitchParser(CodeWriter.BracedWriter writer, List<(EnumMemberModel Member, string Token)> members)
    {
        using var lengthSwitchWriter = writer.StartSwitchCases("switch (input.Length)");
        foreach (var lengthGroup in members.GroupBy(static member => GetInputLength(member.Token, isBytes: true)).OrderBy(static group => group.Key))
        {
            var lengthMembers = lengthGroup.OrderBy(static member => member.Token, StringComparer.Ordinal).ToList();
            var caseValue = lengthGroup.Key.ToString(CultureInfo.InvariantCulture);
            if (lengthMembers.Count <= DirectParserComparisonThreshold)
            {
                lengthSwitchWriter.AddCase(caseValue, caseWriter =>
                {
                    foreach (var member in lengthMembers)
                    {
                        caseWriter.AppendSimpleIf(GetDirectParserCondition(member.Token, isBytes: true), ParserMatch(member.Member));
                    }
                }, "break;");
                continue;
            }

            var asciiMembers = lengthMembers.Where(static member => member.Token.All(static character => character <= 0x7f)).ToList();
            var nonAsciiMembers = lengthMembers.Where(static member => member.Token.Any(static character => character > 0x7f)).ToList();
            lengthSwitchWriter.AddCase(caseValue, caseWriter =>
            {
                caseWriter.OpenBrace();
                if (asciiMembers.Count > 0)
                {
                    caseWriter.AppendLine($"var hash0 = {ParsingHelpersType}.PackAscii(input, 0, ignoreCase);");
                    if (asciiMembers.Any(static member => member.Token.Length > 8))
                    {
                        caseWriter.AppendLine($"var hash1 = {ParsingHelpersType}.PackAscii(input, 8, ignoreCase);");
                    }
                    using (var ignoreCaseWriter = caseWriter.StartBraced("if (ignoreCase)"))
                    {
                        AppendPackedAsciiSwitch(ignoreCaseWriter, DistinctIgnoringCase(asciiMembers).Select(static member => (member.Member, member.Upper)), caseSensitive: false);
                    }
                    using (var caseSensitiveWriter = caseWriter.StartBraced("else"))
                    {
                        AppendPackedAsciiSwitch(caseSensitiveWriter, asciiMembers, caseSensitive: true);
                    }
                }
                foreach (var member in nonAsciiMembers)
                {
                    caseWriter.AppendSimpleIf($"input.SequenceEqual({member.Token.ToCSharpStringLiteral()}u8)", ParserMatch(member.Member));
                }
                caseWriter.AppendLine("break;");
                caseWriter.CloseBrace();
            }, "");
        }
    }

    /// <summary>
    /// <c>switch (hash0)</c> over the tokens' packed first 8 bytes; tokens sharing those bytes (e.g. a common prefix)
    /// share a case and are told apart by their remaining bytes.
    /// </summary>
    private static void AppendPackedAsciiSwitch(CodeWriter.BracedWriter writer, IEnumerable<(EnumMemberModel Member, string Token)> members, bool caseSensitive)
    {
        using var switchWriter = writer.StartSwitchCases("switch (hash0)");
        foreach (var hashGroup in members.GroupBy(member => PackAsciiConstant(member.Token, 0, ignoreCase: !caseSensitive)).OrderBy(static group => group.Key))
        {
            var groupMembers = hashGroup.ToList();
            // A lone token of at most 8 bytes is fully identified by its length and hash0 alone. Anything else ends in
            // a conditional check, so it needs a break (and a lone unconditional match mustn't get one: CS0162).
            var unconditional = groupMembers.Count == 1 && groupMembers[0].Token.Length <= 8;
            switchWriter.AddCase($"{hashGroup.Key.ToString(CultureInfo.InvariantCulture)}UL", caseWriter =>
            {
                foreach (var member in groupMembers)
                {
                    if (unconditional)
                    {
                        caseWriter.AppendLine(ParserMatch(member.Member));
                    }
                    else
                    {
                        caseWriter.AppendSimpleIf(GetPackedRemainderCondition(member.Token, caseSensitive), ParserMatch(member.Member));
                    }
                }
            }, unconditional ? "" : "break;");
        }
    }

    /// <summary>What must hold beyond a matching length and hash0: the next 8 bytes, then any tail compared directly.</summary>
    private static string GetPackedRemainderCondition(string token, bool caseSensitive)
    {
        var conditions = new List<string>();
        if (token.Length > 8)
        {
            conditions.Add($"hash1 == {PackAsciiConstant(token, 8, !caseSensitive).ToString(CultureInfo.InvariantCulture)}UL");
        }
        if (token.Length > 16)
        {
            var tail = token.Substring(16).ToCSharpStringLiteral();
            conditions.Add(caseSensitive ? $"input.Slice(16).SequenceEqual({tail}u8)" : $"{ParsingHelpersType}.AsciiEqualsIgnoreCase(input.Slice(16), {tail}u8)");
        }
        // Two distinct tokens of at most 8 bytes can't share a length and hash0, so there's always a condition here.
        return string.Join(" && ", conditions);
    }

    private static string GetDirectParserCondition(string token, bool isBytes)
    {
        var literal = token.ToCSharpStringLiteral();
        if (!isBytes)
        {
            return $"!ignoreCase ? input.SequenceEqual({literal}.AsSpan()) : input.Equals({literal}.AsSpan(), {StringComparisonAlias}.OrdinalIgnoreCase)";
        }
        return token.All(static character => character <= 0x7f)
            ? $"(!ignoreCase && input.SequenceEqual({literal}u8)) || (ignoreCase && {ParsingHelpersType}.AsciiEqualsIgnoreCase(input, {literal}u8))"
            : $"input.SequenceEqual({literal}u8)";
    }

    private static int GetInputLength(string token, bool isBytes) => isBytes ? global::System.Text.Encoding.UTF8.GetByteCount(token) : token.Length;

    private static ulong PackAsciiConstant(string value, int offset, bool ignoreCase)
    {
        ulong packed = 0;
        for (var index = 0; index < 8 && offset + index < value.Length; index++)
        {
            var character = value[offset + index];
            if (ignoreCase && character is >= 'a' and <= 'z')
            {
                character = (char)(character - 32);
            }
            packed |= (ulong)(byte)character << (index * 8);
        }
        return packed;
    }

    private static string EmitParserHelpers()
    {
        var writer = new CodeWriter(FancyEnumNewFile);
        writer.AppendLine("namespace FancyEnumGenerator.Generated;");
        using (var classWriter = writer.StartBraced("internal static class FancyEnumParsingHelpers"))
        {
            // Per-character invariant upper-casing: the same mapping OrdinalIgnoreCase compares with. The BCL's version is
            // vectorized; older targets get the equivalent loop.
            using (var methodWriter = classWriter.StartBraced("internal static void ToUpperInvariant(global::System.ReadOnlySpan<char> source, global::System.Span<char> destination)"))
            {
                methodWriter.AppendLine("#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER");
                methodWriter.AppendLine("global::System.MemoryExtensions.ToUpperInvariant(source, destination);");
                methodWriter.AppendLine("#else");
                using (var loopWriter = methodWriter.StartBraced("for (var index = 0; index < source.Length; index++)"))
                {
                    loopWriter.AppendLine("destination[index] = char.ToUpperInvariant(source[index]);");
                }
                methodWriter.AppendLine("#endif");
            }
            using (var methodWriter = classWriter.StartBraced("internal static ulong PackAscii(global::System.ReadOnlySpan<byte> value, int offset, bool ignoreCase)"))
            {
                methodWriter.AppendLine("ulong packed = 0;");
                using (var loopWriter = methodWriter.StartBraced("for (var index = 0; index < 8 && offset + index < value.Length; index++)"))
                {
                    loopWriter.AppendLine("var current = value[offset + index];");
                    loopWriter.AppendSimpleIf("ignoreCase && current is >= (byte)'a' and <= (byte)'z'", "current -= 32;");
                    loopWriter.AppendLine("packed |= (ulong)current << (index * 8);");
                }
                methodWriter.AppendLine("return packed;");
            }
            using (var methodWriter = classWriter.StartBraced("internal static bool AsciiEqualsIgnoreCase(global::System.ReadOnlySpan<byte> left, global::System.ReadOnlySpan<byte> right)"))
            {
                methodWriter.AppendSimpleIf("left.Length != right.Length", "return false;");
                using (var loopWriter = methodWriter.StartBraced("for (var index = 0; index < left.Length; index++)"))
                {
                    loopWriter.AppendLine("var x = left[index];");
                    loopWriter.AppendLine("var y = right[index];");
                    loopWriter.AppendSimpleIf("x is >= (byte)'a' and <= (byte)'z'", "x -= 32;");
                    loopWriter.AppendSimpleIf("y is >= (byte)'a' and <= (byte)'z'", "y -= 32;");
                    loopWriter.AppendSimpleIf("x != y", "return false;");
                }
                methodWriter.AppendLine("return true;");
            }
            using (var methodWriter = classWriter.StartBraced("internal static bool AsciiStartsWithCombined(global::System.ReadOnlySpan<byte> value, global::System.ReadOnlySpan<byte> prefix, global::System.ReadOnlySpan<byte> next, bool ignoreCase)"))
            {
                methodWriter.AppendSimpleIf("prefix.Length + next.Length > value.Length", "return false;");
                methodWriter.AppendLine("var valueIndex = 0;");
                using (var loopWriter = methodWriter.StartBraced("for (var index = 0; index < prefix.Length; index++, valueIndex++)"))
                {
                    loopWriter.AppendLine("var expected = value[valueIndex];");
                    loopWriter.AppendLine("var actual = prefix[index];");
                    loopWriter.AppendSimpleIf("ignoreCase && expected is >= (byte)'a' and <= (byte)'z'", "expected -= 32;");
                    loopWriter.AppendSimpleIf("ignoreCase && actual is >= (byte)'a' and <= (byte)'z'", "actual -= 32;");
                    loopWriter.AppendSimpleIf("expected != actual", "return false;");
                }
                using (var loopWriter = methodWriter.StartBraced("for (var index = 0; index < next.Length; index++, valueIndex++)"))
                {
                    loopWriter.AppendLine("var expected = value[valueIndex];");
                    loopWriter.AppendLine("var actual = next[index];");
                    loopWriter.AppendSimpleIf("ignoreCase && expected is >= (byte)'a' and <= (byte)'z'", "expected -= 32;");
                    loopWriter.AppendSimpleIf("ignoreCase && actual is >= (byte)'a' and <= (byte)'z'", "actual -= 32;");
                    loopWriter.AppendSimpleIf("expected != actual", "return false;");
                }
                methodWriter.AppendLine("return true;");
            }
        }
        return writer.AsString();
    }
}
