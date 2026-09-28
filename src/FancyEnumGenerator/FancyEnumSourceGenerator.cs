using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FancyEnumGenerator;

/// <summary>
/// The FancyEnum incremental source generator. It is loaded by the compiler from the package's analyzer folder and
/// isn't meant to be referenced or called from user code; see the attributes in <c>FancyEnumGenerator.Attributes</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class FancyEnumSourceGenerator : IIncrementalGenerator
{
    private static readonly string EnumAttributeName = typeof(FancyEnumAttribute).FullName!;
    private static readonly string MemberSetAttributeName = typeof(FancyEnumMemberSetAttribute).FullName!;

    // These are well-known BCL/framework attributes, so unlike our own attributes there's no need to
    // resolve them via GetTypeByMetadataName (and no INamedTypeSymbol? null case to worry about
    // either) - a plain display-name check via HasAttribute/TryGetAttribute (see
    // FancyEnumGenerator.Internal.RoslynSymbolExtensions) is simpler and just as correct. This is safe
    // even for JsonStringEnumMemberNameAttribute, which isn't available on every target: if the type
    // genuinely doesn't exist in a consumer's compilation, they could never have applied it to a
    // member in the first place, so the string match simply never finds one - the same as "nobody set
    // the attribute" - not a crash or a special case to guard against.
    private const string FlagsAttributeFullName = "System.FlagsAttribute";
    private const string ObsoleteAttributeFullName = "System.ObsoleteAttribute";
    private const string DescriptionAttributeFullName = "System.ComponentModel.DescriptionAttribute";
    private const string DisplayAttributeFullName = "System.ComponentModel.DataAnnotations.DisplayAttribute";
    private const string EnumMemberAttributeFullName = "System.Runtime.Serialization.EnumMemberAttribute";
    private const string JsonStringEnumMemberNameAttributeFullName = "System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute";

    /// <summary>
    /// Every attribute-type symbol lookup and the assembly-level <see cref="FancyEnumDefaultsAttribute"/> lookup
    /// depend only on the <see cref="Compilation"/>, never on the specific enum being processed. Resolving them
    /// once per compilation here (via <see cref="IncrementalGeneratorInitializationContext.CompilationProvider"/>)
    /// instead of once per enum inside <see cref="Extract"/> avoids redoing the same symbol lookups for every
    /// enum in the project on every generator pass.
    /// </summary>
    private sealed record WellKnownTypes
    {
        public required INamedTypeSymbol? MappingSettingsAttributeType { get; init; }
        public required INamedTypeSymbol? TypedMappingSettingsAttributeType { get; init; }
        public required INamedTypeSymbol? StringMemberMappingAttributeType { get; init; }
        public required INamedTypeSymbol? GenericMemberMappingAttributeType { get; init; }
        public required INamedTypeSymbol? MemberSetAttributeType { get; init; }
        public required INamedTypeSymbol? MemberSetItemAttributeType { get; init; }
        public required INamedTypeSymbol? ConstructorMappingAttributeType { get; init; }
        public required INamedTypeSymbol? MemberSettingsAttributeType { get; init; }
        public required AttributeData? AssemblyDefaults { get; init; }
    }

    private static WellKnownTypes GetWellKnownTypes(Compilation compilation, CancellationToken _)
    {
        var defaultsAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumDefaultsAttribute).FullName!);
        return new WellKnownTypes
        {
            MappingSettingsAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberMappingSettingsAttribute).FullName!),
            TypedMappingSettingsAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberMappingSettingsAttribute<>).FullName!),
            StringMemberMappingAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberAttribute).FullName!),
            GenericMemberMappingAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberAttribute<>).FullName!),
            MemberSetAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberSetAttribute).FullName!),
            MemberSetItemAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberSetItemAttribute).FullName!),
            ConstructorMappingAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumConstructorMappingAttribute).FullName!),
            MemberSettingsAttributeType = compilation.GetTypeByMetadataName(typeof(FancyEnumMemberSettingsAttribute).FullName!),
            AssemblyDefaults = compilation.Assembly.GetAttributes().FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, defaultsAttributeType))
        };
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext => postInitializationContext.AddSource("FancyEnumParsingHelpers.cs", EmitParserHelpers()));

        var configOptions = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => provider.GlobalOptions);
        var wellKnownTypes = context.CompilationProvider.Select(static (compilation, cancellationToken) => GetWellKnownTypes(compilation, cancellationToken));

        // Member-set attribute classes are analyzed once each, not once per enum using them: their diagnostics are
        // reported exactly once, at the class, and even before any enum uses it. Enums then look shapes up by name.
        var memberSetShapes = context.SyntaxProvider.ForAttributeWithMetadataName(
                MemberSetAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, _) => attributeContext)
            .Combine(wellKnownTypes)
            .Select(static (pair, _) => BuildMemberSetShape((INamedTypeSymbol)pair.Left.TargetSymbol, pair.Right));
        context.RegisterSourceOutput(memberSetShapes, static (sourceContext, shape) =>
        {
            foreach (var diagnostic in shape.Diagnostics)
            {
                sourceContext.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
        });
        var knownShapes = memberSetShapes.Collect().Select(static (shapes, _) => shapes.ToEquatableArray());

        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
                EnumAttributeName,
                static (node, _) => node is EnumDeclarationSyntax,
                static (attributeContext, _) => attributeContext)
            .Combine(configOptions.Combine(wellKnownTypes).Combine(knownShapes))
            .Select(static (pair, _) => Extract((INamedTypeSymbol)pair.Left.TargetSymbol, pair.Left.Attributes[0], pair.Right.Left.Left, pair.Right.Left.Right, pair.Right.Right));

        context.RegisterSourceOutput(models.Select(static (model, _) => Emit(model)), static (sourceContext, result) => WriteResult(sourceContext, result));
    }

    private static void WriteResult(SourceProductionContext sourceContext, GenResult result)
    {
        if (string.IsNullOrWhiteSpace(result.SourceCode) == false)
        {
            sourceContext.AddSource(result.FileName!, result.SourceCode);
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            sourceContext.ReportDiagnostic(diagnostic.ToDiagnostic());
        }
    }

    private static EnumModel Extract(INamedTypeSymbol symbol, AttributeData enumAttribute, AnalyzerConfigOptions globalOptions, WellKnownTypes wellKnownTypes, EquatableArray<MemberSetShapeModel> knownShapes)
    {
        var mappingSettingsAttributeType = wellKnownTypes.MappingSettingsAttributeType;
        var typedMappingSettingsAttributeType = wellKnownTypes.TypedMappingSettingsAttributeType;
        var stringMemberMappingAttributeType = wellKnownTypes.StringMemberMappingAttributeType;
        var genericMemberMappingAttributeType = wellKnownTypes.GenericMemberMappingAttributeType;
        var memberSettingsAttributeType = wellKnownTypes.MemberSettingsAttributeType;

        // Shapes declared in this compilation come pre-analyzed from the member-set pipeline. One declared in a
        // referenced assembly (or a constructed generic attribute) isn't in that set, so it's analyzed here from its
        // symbol; its diagnostics are dropped, since they belong to the compilation that declares it.
        var shapeCache = new Dictionary<string, MemberSetShapeModel?>(StringComparer.Ordinal);
        MemberSetShapeModel? ResolveShape(INamedTypeSymbol? attributeClass)
        {
            if (attributeClass is null)
            {
                return null;
            }
            var name = attributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!shapeCache.TryGetValue(name, out var shape))
            {
                shape = knownShapes.FirstOrDefault(known => string.Equals(known.FullyQualifiedName, name, StringComparison.Ordinal))
                    ?? (IsMemberSet(attributeClass, wellKnownTypes.MemberSetAttributeType) ? BuildMemberSetShape(attributeClass, wellKnownTypes) : null);
                shapeCache[name] = shape;
            }
            return shape;
        }
        var isFlags = symbol.HasAttribute(FlagsAttributeFullName);
        var assemblyDefaults = wellKnownTypes.AssemblyDefaults;
        var diagnostics = new List<DiagnosticInfo>();
        var containingTypes = new List<INamedTypeSymbol>();
        for (var containingType = symbol.ContainingType; containingType is not null; containingType = containingType.ContainingType)
        {
            containingTypes.Insert(0, containingType);
        }
        if (containingTypes.Any(static containingType => containingType.TypeParameters.Length > 0))
        {
            diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.UnsupportedEnumContainer, symbol.Locations.FirstOrDefault(), symbol.ToDisplayString(), "generic containing types are not supported"));
        }
        else if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal) || containingTypes.Any(static containingType => containingType.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)))
        {
            diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.UnsupportedEnumContainer, symbol.Locations.FirstOrDefault(), symbol.ToDisplayString(), "the enum is not accessible from its namespace"));
        }

        var mappingSettingSources = new List<(AttributeData Attribute, Location? Location)>();
        mappingSettingSources.AddRange(symbol.GetAttributes().Where(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, mappingSettingsAttributeType) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, typedMappingSettingsAttributeType))
            .Select(attribute => (attribute, attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation())));

        var mappingSettings = new List<MappingSettingsModel>();
        foreach (var (attribute, location) in mappingSettingSources)
        {
            var fieldName = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "";
            if (mappingSettings.Any(setting => string.Equals(setting.FieldName, fieldName, StringComparison.Ordinal)))
            {
                diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.InvalidMapping, location, symbol.ToDisplayString(), fieldName, "mapping settings are declared more than once"));
                continue;
            }

            if (attribute.AttributeClass is { IsGenericType: true } typedSettingsAttribute)
            {
                var returnType = FormatReturnType(typedSettingsAttribute.TypeArguments[0]);
                var hasNotDefined = attribute.TryReadNamedTypedConstant(nameof(FancyEnumMemberMappingSettingsAttribute<int>.NotDefined), out var notDefined);
                var hasNotMatched = attribute.TryReadNamedTypedConstant(nameof(FancyEnumMemberMappingSettingsAttribute<int>.NotMatched), out var notMatched);
                mappingSettings.Add(new MappingSettingsModel
                {
                    FieldName = fieldName,
                    Location = GeneratorLocationInfo.FromLocation(location),
                    ReturnType = returnType,
                    IsNonStringReferenceType = IsNonStringReferenceType(typedSettingsAttribute.TypeArguments[0]),
                    NotDefinedExpression = hasNotDefined ? FormatConstant(notDefined, returnType) : null,
                    NotMatchedExpression = hasNotMatched ? FormatConstant(notMatched, returnType) : null,
                    NotDefinedStringValue = hasNotDefined ? notDefined.Value as string : null,
                    NotMatchedStringValue = hasNotMatched ? notMatched.Value as string : null,
                    ReturnNullOnNotMatched = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute<int>.ReturnNullOnNotMatched)),
                    ThrowOnNotMatched = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute<int>.ThrowOnNotMatched)),
                    CreateTryFormat = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute<int>.CreateTryFormat)),
                    IncludeUtf8Value = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute<int>.IncludeUtf8Value)),
                    ParseFrom = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute<int>.ParseFrom)),
                    ParseCaseSensitive = ReadOptionalBool(attribute, nameof(FancyEnumMemberMappingSettingsAttribute<int>.ParseCaseSensitive))
                });
            }
            else
            {
                var notMatched = attribute.TryReadNamedString(nameof(FancyEnumMemberMappingSettingsAttribute.NotMatched));
                mappingSettings.Add(new MappingSettingsModel
                {
                    FieldName = fieldName,
                    Location = GeneratorLocationInfo.FromLocation(location),
                    NotDefined = attribute.TryReadNamedEnum<FancyEnumMemberFallbackOption>(nameof(FancyEnumMemberMappingSettingsAttribute.NotDefined), out var notDefined) ? notDefined : default,
                    NotMatchedExpression = notMatched?.ToCSharpStringLiteral(),
                    NotMatchedStringValue = notMatched,
                    ReturnNullOnNotMatched = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute.ReturnNullOnNotMatched)),
                    ThrowOnNotMatched = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute.ThrowOnNotMatched)),
                    ParseFrom = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute.ParseFrom)),
                    ParseCaseSensitive = ReadOptionalBool(attribute, nameof(FancyEnumMemberMappingSettingsAttribute.ParseCaseSensitive)),
                    CreateTryFormat = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute.CreateTryFormat)),
                    IncludeUtf8Value = attribute.TryReadNamedBool(nameof(FancyEnumMemberMappingSettingsAttribute.IncludeUtf8Value))
                });
            }
        }

        // Member-set shapes contribute lower-precedence settings, keyed by field name: an explicit enum-level
        // FancyEnumMemberMappingSettingsAttribute for the same field always wins (skipped silently below, same
        // as today's "first one wins" rule for the explicit sources above).
        foreach (var shape in symbol.GetMembers().OfType<IFieldSymbol>()
            .SelectMany(static field => field.GetAttributes())
            .Select(attribute => ResolveShape(attribute.AttributeClass))
            .OfType<MemberSetShapeModel>()
            .Distinct())
        {
            foreach (var field in shape.Fields)
            {
                if (!mappingSettings.Any(setting => string.Equals(setting.FieldName, field.FieldName, StringComparison.Ordinal)))
                {
                    mappingSettings.Add(field.Settings);
                }
            }
        }

        var includeObsolete = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.IncludeObsolete), false);
        var members = new List<EnumMemberModel>();
        foreach (var field in symbol.GetMembers().OfType<IFieldSymbol>().Where(static field => field.HasConstantValue))
        {
            var attributes = field.GetAttributes();
            if (!includeObsolete && attributes.Any(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), ObsoleteAttributeFullName, StringComparison.Ordinal)))
            {
                continue;
            }

            var description = attributes.FirstOrDefault(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), DescriptionAttributeFullName, StringComparison.Ordinal))?.ConstructorArguments.FirstOrDefault().Value as string;
            var displayName = attributes.FirstOrDefault(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), DisplayAttributeFullName, StringComparison.Ordinal)).TryReadNamedString("Name");
            var enumMemberValue = attributes.FirstOrDefault(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), EnumMemberAttributeFullName, StringComparison.Ordinal)).TryReadNamedString("Value");
            var jsonStringEnumMemberName = attributes.FirstOrDefault(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), JsonStringEnumMemberNameAttributeFullName, StringComparison.Ordinal))?.ConstructorArguments.FirstOrDefault().Value as string;
            var excludeFromValues = attributes.FirstOrDefault(attribute => IsOrDerivesFrom(attribute.AttributeClass, memberSettingsAttributeType)).TryReadNamedBool(nameof(FancyEnumMemberSettingsAttribute.ExcludeFromValues));
            var mappings = new List<MemberMappingModel>();
            foreach (var attribute in attributes)
            {
                if (attribute.AttributeClass is { } memberMappingAttributeType &&
                    (SymbolEqualityComparer.Default.Equals(memberMappingAttributeType, stringMemberMappingAttributeType) ||
                     SymbolEqualityComparer.Default.Equals(memberMappingAttributeType.OriginalDefinition, genericMemberMappingAttributeType)))
                {
                    var returnType = memberMappingAttributeType.IsGenericType ? FormatReturnType(memberMappingAttributeType.TypeArguments[0]) : "string";
                    mappings.Add(ExtractMemberMapping(attribute, returnType) with { IsNonStringReferenceType = memberMappingAttributeType.IsGenericType && IsNonStringReferenceType(memberMappingAttributeType.TypeArguments[0]) });
                }
                else if (ResolveShape(attribute.AttributeClass) is { } shape)
                {
                    var constructor = attribute.AttributeConstructor is { } boundConstructor
                        ? shape.Constructors.FirstOrDefault(candidate => string.Equals(candidate.Signature, ConstructorSignature(boundConstructor), StringComparison.Ordinal))
                        : null;
                    foreach (var shapeField in shape.Fields)
                    {
                        // A named argument always wins (matches real C# semantics: named property assignments
                        // apply after the constructor runs). Otherwise fall back to whichever parameter of the
                        // constructor actually resolved for THIS application maps to this property, if any.
                        TypedConstant value;
                        if (attribute.TryReadNamedTypedConstant(shapeField.PropertyName, out value))
                        {
                            // use it
                        }
                        else if (constructor is not null && constructor.ParameterProperties.AsSpan().IndexOf(shapeField.PropertyName) is >= 0 and var parameterIndex)
                        {
                            value = attribute.ConstructorArguments.ElementAtOrDefault(parameterIndex);
                        }
                        else
                        {
                            continue;
                        }
                        mappings.Add(new MemberMappingModel { FieldName = shapeField.FieldName, ReturnType = shapeField.ReturnType, IsNonStringReferenceType = shapeField.Settings.IsNonStringReferenceType, Expression = FormatConstant(value, shapeField.ReturnType), StringValue = value.Value as string });
                    }
                }
            }

            foreach (var duplicate in mappings.GroupBy(static mapping => mapping.FieldName, StringComparer.Ordinal).Where(static group => group.Count() > 1))
            {
                diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.InvalidMapping, field.Locations.FirstOrDefault(), symbol.ToDisplayString(), duplicate.Key, $"member '{field.Name}' declares the field more than once"));
            }

            members.Add(new EnumMemberModel
            {
                Name = field.Name,
                Description = description,
                DisplayName = displayName,
                EnumMemberValue = enumMemberValue,
                JsonStringEnumMemberName = jsonStringEnumMemberName,
                NumericValue = Convert.ToDecimal(field.ConstantValue, CultureInfo.InvariantCulture),
                ExcludeFromValues = excludeFromValues,
                Mappings = mappings.GroupBy(static mapping => mapping.FieldName, StringComparer.Ordinal).Select(static group => group.First()).ToEquatableArray(),
                Location = GeneratorLocationInfo.FromLocation(field.Locations.FirstOrDefault())
            });
        }

        var enumName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var generatedName = string.Join("_", containingTypes.Select(static containingType => containingType.Name).Append(symbol.Name)).ToSafeCSharpIdentifier();
        foreach (var duplicate in members.GroupBy(static member => member.NumericValue).Where(static group => group.Count() > 1))
        {
            diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.DuplicateNumericValue, duplicate.First().Location, enumName, duplicate.Key));
        }
        var underlyingType = symbol.EnumUnderlyingType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (isFlags)
        {
            foreach (var member in members.Where(member => !member.ExcludeFromValues && member.NumericValue != 0 && !IsSingleBit(member.NumericValue, underlyingType)))
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.InvalidFlagsValue, member.Location, enumName, member.Name, member.NumericValue.ToString(CultureInfo.InvariantCulture)));
            }
        }

        var allowNoUnknown = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.AllowNoUnknown), false);
        if (!allowNoUnknown && !members.Any(IsUnknownMember))
        {
            diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.MissingUnknown, symbol.Locations.FirstOrDefault(), enumName));
        }

        // Flag values (1, 2, 4, ...) are non-contiguous by construction, so for [Flags] enums the library default flips
        // to allowing it; an explicit setting at any level still wins.
        var allowNonContiguous = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.AllowNonContiguous), isFlags);
        var numericValues = members.Select(static member => member.NumericValue).Distinct().OrderBy(static value => value).ToArray();
        if (!allowNonContiguous && numericValues.Skip(1).Where((value, index) => value != numericValues[index] + 1).Any())
        {
            diagnostics.Add(new DiagnosticInfo(EnumGeneratorDiagnostics.NonContiguous, symbol.Locations.FirstOrDefault(), enumName));
        }

        return new EnumModel
        {
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString(),
            Name = symbol.Name,
            GeneratedName = generatedName,
            HintName = symbol.ToDisplayString().ToSafeHintName(),
            FullyQualifiedName = enumName,
            UnderlyingType = underlyingType,
            Accessibility = symbol.DeclaredAccessibility == Accessibility.Public && containingTypes.All(static containingType => containingType.DeclaredAccessibility == Accessibility.Public) ? "public" : "internal",
            NoInlineArray = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.NoInlineArray), false),
            CreateStaticReadonlyCollection = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.CreateStaticReadonlyCollection), false),
            AllowNonContiguous = allowNonContiguous,
            AllowNoUnknown = allowNoUnknown,
            IncludeObsolete = includeObsolete,
            DefaultToStringBehavior = ResolveEnumOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.DefaultToStringBehavior), default(FancyEnumDefaultToStringBehavior)),
            DefaultToStringCustomField = ResolveStringOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.DefaultToStringCustomField)),
            CreateTryFormat = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.CreateTryFormat), false),
            CreateByteParsing = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.CreateByteParsing), false),
            CreateIsValidPrefix = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.CreateIsValidPrefix), false),
            ParseCaseSensitive = ResolveBoolOption(enumAttribute, assemblyDefaults, globalOptions, nameof(FancyEnumAttribute.ParseCaseSensitive), true),
            GenerateParseMethods = ResolveBoolOption(null, assemblyDefaults, globalOptions, nameof(FancyEnumDefaultsAttribute.GenerateParseMethods), true),
            UseGeneratedFileSuffix = ResolveBoolOption(null, assemblyDefaults, globalOptions, nameof(FancyEnumDefaultsAttribute.UseGeneratedFileSuffix), true),
            IsFlags = isFlags,
            Members = members.ToEquatableArray(),
            MappingSettings = mappingSettings.ToEquatableArray(),
            Diagnostics = diagnostics.ToEquatableArray()
        };
    }

    private static bool IsSingleBit(decimal value, string underlyingType)
    {
        if (value < 0)
        {
            return underlyingType switch
            {
                "sbyte" => value == sbyte.MinValue,
                "short" => value == short.MinValue,
                "int" => value == int.MinValue,
                "long" => value == long.MinValue,
                _ => false
            };
        }

        if (value <= 0 || decimal.Truncate(value) != value)
        {
            return false;
        }
        while (value % 2 == 0)
        {
            value /= 2;
        }
        return value == 1;
    }

    private static bool IsUnknownMember(EnumMemberModel member) => member.NumericValue == 0 && string.Equals(member.Name, "unknown", StringComparison.OrdinalIgnoreCase);

    private static bool IsOrDerivesFrom(INamedTypeSymbol? type, INamedTypeSymbol? expectedType)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(type, expectedType))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsNonStringReferenceType(ITypeSymbol type) => type.IsReferenceType && type.SpecialType != SpecialType.System_String;

    /// <summary>The C# type name to embed in generated code for a mapping's declared return type.</summary>
    private static string FormatReturnType(ITypeSymbol symbol) => symbol.SpecialType == SpecialType.System_String ? "string" : symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static MemberMappingModel ExtractMemberMapping(AttributeData attribute, string returnType)
    {
        var fieldName = attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "";
        var staticMethodName = attribute.TryReadNamedString(nameof(FancyEnumMemberAttribute<object>.StaticMethodName));
        var staticMethodSource = attribute.TryReadNamedTypedConstant(nameof(FancyEnumMemberAttribute<object>.StaticMethodSource), out var staticMethodSourceConstant) ? staticMethodSourceConstant.Value as INamedTypeSymbol : null;
        if (!attribute.TryReadNamedTypedConstant(nameof(FancyEnumMemberAttribute<object>.Value), out var value) && attribute.ConstructorArguments.Length > 1)
        {
            value = attribute.ConstructorArguments[1];
        }
        var expression = staticMethodName is not null && staticMethodSource is not null
            ? StaticMemberExpression(staticMethodSource, staticMethodName)
            : FormatConstant(value, returnType);
        return new MemberMappingModel { FieldName = fieldName, ReturnType = returnType, Expression = expression, StringValue = staticMethodSource is null ? value.Value as string : null, HasStaticMemberSource = staticMethodSource is not null };
    }

    private static string StaticMemberExpression(INamedTypeSymbol source, string memberName)
    {
        var member = source.GetMembers(memberName).FirstOrDefault(static member => member is IFieldSymbol { IsStatic: true, IsReadOnly: true } or IMethodSymbol { IsStatic: true, Parameters.Length: 0 });
        var suffix = member is IFieldSymbol ? "" : "()";
        return $"{source.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{memberName}{suffix}";
    }

    /// <summary>
    /// Resolves a bool option through, in order: an explicit value on the enum's own <see cref="FancyEnumAttribute"/>
    /// (or mapping-settings attribute), an assembly-wide <see cref="FancyEnumDefaultsAttribute"/>, an MSBuild
    /// <c>FancyEnum{propertyName}</c> property, and finally <paramref name="hardcodedFallback"/>.
    /// </summary>
    private static bool ResolveBoolOption(AttributeData? perEnumAttribute, AttributeData? assemblyDefaults, AnalyzerConfigOptions globalOptions, string propertyName, bool hardcodedFallback)
    {
        if (perEnumAttribute.TryReadNamedBool(propertyName, out var perEnumValue))
        {
            return perEnumValue!.Value;
        }
        if (assemblyDefaults.TryReadNamedBool(propertyName, out var assemblyValue))
        {
            return assemblyValue!.Value;
        }
        if (globalOptions.TryGetValue($"build_property.FancyEnum{propertyName}", out var raw) && bool.TryParse(raw, out var msBuildValue))
        {
            return msBuildValue;
        }
        return hardcodedFallback;
    }

    /// <summary>A bool named argument if it was written explicitly, else <see langword="null"/> (so the caller can inherit a default).</summary>
    private static bool? ReadOptionalBool(AttributeData? attribute, string propertyName) =>
        attribute.TryReadNamedBool(propertyName, out bool? value) ? value : null;

    /// <summary>Same precedence as <see cref="ResolveBoolOption"/>, for enum-valued options.</summary>
    private static TEnum ResolveEnumOption<TEnum>(AttributeData? perEnumAttribute, AttributeData? assemblyDefaults, AnalyzerConfigOptions globalOptions, string propertyName, TEnum hardcodedFallback)
        where TEnum : struct, Enum
    {
        if (perEnumAttribute.TryReadNamedEnum<TEnum>(propertyName, out var perEnumValue))
        {
            return perEnumValue;
        }
        if (assemblyDefaults.TryReadNamedEnum<TEnum>(propertyName, out var assemblyValue))
        {
            return assemblyValue;
        }
        if (globalOptions.TryGetValue($"build_property.FancyEnum{propertyName}", out var raw) && Enum.TryParse(raw, ignoreCase: true, out TEnum msBuildValue))
        {
            return msBuildValue;
        }
        return hardcodedFallback;
    }

    /// <summary>Same precedence as <see cref="ResolveBoolOption"/>, for string-valued options; an empty MSBuild value counts as unset.</summary>
    private static string? ResolveStringOption(AttributeData? perEnumAttribute, AttributeData? assemblyDefaults, AnalyzerConfigOptions globalOptions, string propertyName)
    {
        if (perEnumAttribute.TryReadNamedString(propertyName) is { } perEnumValue)
        {
            return perEnumValue;
        }
        if (assemblyDefaults.TryReadNamedString(propertyName) is { } assemblyValue)
        {
            return assemblyValue;
        }
        return globalOptions.TryGetValue($"build_property.FancyEnum{propertyName}", out var raw) && !string.IsNullOrWhiteSpace(raw) ? raw : null;
    }

    /// <summary>A C# expression for an attribute argument's constant value.</summary>
    /// <remarks>
    /// Numbers are cast to the constant's own type rather than the field's, so an <c>object</c>-typed field boxes
    /// what was actually written (an enum, a char) instead of its raw number, and the number is parenthesized so a
    /// negative value after a non-keyword type (<c>(global::Ns.Color)(-1)</c>) stays a cast, not a subtraction.
    /// </remarks>
    private static string FormatConstant(TypedConstant value, string returnType)
    {
        if (value.IsNull)
        {
            return "default";
        }
        switch (value.Value)
        {
            case string text:
                return text.ToCSharpStringLiteral();
            case bool boolean:
                return boolean ? "true" : "false";
            case ITypeSymbol type:
                return $"typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})";
        }
        var castType = value.Type is { } constantType ? FormatReturnType(constantType) : returnType;
        var number = value.Value switch
        {
            char character => ((int)character).ToString(CultureInfo.InvariantCulture),
            float single => FormatFloatingPoint(float.IsNaN(single), float.IsPositiveInfinity(single), float.IsNegativeInfinity(single), "float", single.ToString("R", CultureInfo.InvariantCulture) + "F"),
            double @double => FormatFloatingPoint(double.IsNaN(@double), double.IsPositiveInfinity(@double), double.IsNegativeInfinity(@double), "double", @double.ToString("R", CultureInfo.InvariantCulture) + "D"),
            _ => Convert.ToString(value.Value, CultureInfo.InvariantCulture)
        };
        return $"({castType})({number})";
    }

    /// <summary>"R" round-trips on every runtime the compiler runs on (plain ToString doesn't on .NET Framework); non-finite values have no literal.</summary>
    private static string FormatFloatingPoint(bool isNaN, bool isPositiveInfinity, bool isNegativeInfinity, string keyword, string literal) =>
        isNaN ? $"{keyword}.NaN" : isPositiveInfinity ? $"{keyword}.PositiveInfinity" : isNegativeInfinity ? $"{keyword}.NegativeInfinity" : literal;

}
