using Microsoft.CodeAnalysis;

namespace FancyEnumGenerator;

/// <summary>Analysis of <see cref="FancyEnumMemberSetAttribute"/>-marked attribute classes ("shapes").</summary>
public sealed partial class FancyEnumSourceGenerator
{
    private static bool IsMemberSet(INamedTypeSymbol? attributeType, INamedTypeSymbol? memberSetAttributeType) =>
        attributeType is not null && attributeType.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, memberSetAttributeType));

    /// <summary>
    /// Analyzes a member-set shape: which of its properties become fields (and with what fallback settings), how each
    /// public constructor's parameters feed them, and every problem with the class itself.
    /// </summary>
    private static MemberSetShapeModel BuildMemberSetShape(INamedTypeSymbol shapeType, WellKnownTypes wellKnownTypes)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var shapeName = shapeType.ToDisplayString();
        var properties = GetShapeProperties(shapeType);

        var constructors = new List<MemberSetConstructorModel>();
        foreach (var constructor in shapeType.Constructors.Where(static constructor => !constructor.IsStatic && constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length > 0))
        {
            var parameterProperties = new List<string>();
            foreach (var parameter in constructor.Parameters)
            {
                var property = properties.FirstOrDefault(property => ParameterMapsToProperty(parameter, property, wellKnownTypes.ConstructorMappingAttributeType));
                if (property is null)
                {
                    // Validated for every public constructor, whether or not any enum uses that overload yet: an
                    // unresolvable parameter would silently lose its value.
                    diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.MemberSetConstructorParameterUnresolved, parameter.Locations.FirstOrDefault(), parameter.Name, shapeName));
                }
                parameterProperties.Add(property?.Name ?? "");
            }
            constructors.Add(new MemberSetConstructorModel { Signature = ConstructorSignature(constructor), ParameterProperties = parameterProperties.ToEquatableArray() });
        }

        var fields = new List<MemberSetFieldModel>();
        foreach (var property in properties)
        {
            var item = property.GetAttributes().FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, wellKnownTypes.MemberSetItemAttributeType));
            if (item.TryReadNamedBool(nameof(FancyEnumMemberSetItemAttribute.Ignore)))
            {
                continue;
            }
            var location = property.Locations.FirstOrDefault();
            if (UnsupportedMemberSetTypeReason(property.Type) is { } unsupportedReason)
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.MemberSetPropertyUnusable, location, shapeName, property.Name, $"has type '{property.Type.ToDisplayString()}', which {unsupportedReason}"));
                continue;
            }
            if (!IsPubliclySettable(property) && !constructors.Any(constructor => constructor.ParameterProperties.AsSpan().IndexOf(property.Name) >= 0))
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.MemberSetPropertyUnusable, location, shapeName, property.Name, "is read-only and no public constructor parameter maps to it, so no enum member could ever set it"));
                continue;
            }

            var fieldName = item.TryReadNamedString(nameof(FancyEnumMemberSetItemAttribute.Name)) ?? property.Name;
            var returnType = FormatReturnType(property.Type);
            var defaultValue = ReadMatchingValue(item, nameof(FancyEnumMemberSetItemAttribute.DefaultValue), property, shapeName, diagnostics);
            var notMatched = ReadMatchingValue(item, nameof(FancyEnumMemberSetItemAttribute.NotMatched), property, shapeName, diagnostics);
            var notDefined = item.TryReadNamedEnum<FancyEnumMemberFallbackOption>(nameof(FancyEnumMemberSetItemAttribute.NotDefined), out var fallback) ? fallback : default;
            if (notDefined != FancyEnumMemberFallbackOption.Skip && property.Type.SpecialType != SpecialType.System_String)
            {
                diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.MemberSetValueTypeMismatch, location,
                    shapeName, property.Name, property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "string", nameof(FancyEnumMemberSetItemAttribute.NotDefined)));
                notDefined = default;
            }
            fields.Add(new MemberSetFieldModel
            {
                PropertyName = property.Name,
                FieldName = fieldName,
                ReturnType = returnType,
                Settings = new MappingSettingsModel
                {
                    FieldName = fieldName,
                    ReturnType = returnType,
                    IsNonStringReferenceType = IsNonStringReferenceType(property.Type),
                    NotDefined = notDefined,
                    NotDefinedExpression = defaultValue is { } notDefinedValue ? FormatConstant(notDefinedValue, returnType) : null,
                    NotDefinedStringValue = defaultValue?.Value as string,
                    NotMatchedExpression = notMatched is { } notMatchedValue ? FormatConstant(notMatchedValue, returnType) : null,
                    NotMatchedStringValue = notMatched?.Value as string,
                    ReturnNullOnNotMatched = item.TryReadNamedBool(nameof(FancyEnumMemberSetItemAttribute.ReturnNullOnNotMatched)),
                    ThrowOnNotMatched = item.TryReadNamedBool(nameof(FancyEnumMemberSetItemAttribute.ThrowOnNotMatched)),
                    ParseFrom = item.TryReadNamedBool(nameof(FancyEnumMemberSetItemAttribute.ParseFrom)),
                    ParseCaseSensitive = ReadOptionalBool(item, nameof(FancyEnumMemberSetItemAttribute.ParseCaseSensitive)),
                    CreateTryFormat = item.TryReadNamedBool(nameof(FancyEnumMemberSetItemAttribute.CreateTryFormat)),
                    IncludeUtf8Value = item.TryReadNamedBool(nameof(FancyEnumMemberSetItemAttribute.IncludeUtf8Value))
                }
            });
        }

        return new MemberSetShapeModel
        {
            FullyQualifiedName = shapeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Fields = fields.ToEquatableArray(),
            Constructors = constructors.ToEquatableArray(),
            Diagnostics = diagnostics.ToEquatableArray()
        };
    }

    /// <summary>
    /// Every public instance property of a shape, including inherited ones (up to, not including,
    /// <see cref="Attribute"/>) - readable or not, since a read-only property's value can still come from a
    /// constructor parameter. Most-derived first, so an override or <c>new</c> property hides the base one.
    /// </summary>
    private static IReadOnlyList<IPropertySymbol> GetShapeProperties(INamedTypeSymbol shapeType)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var properties = new List<IPropertySymbol>();
        for (var type = shapeType; type is not null && type.SpecialType != SpecialType.System_Object && type.ToDisplayString() != "System.Attribute"; type = type.BaseType)
        {
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public && seen.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }
        return properties;
    }

    /// <summary>Whether a named attribute argument can set this property: a public setter here or on a property it overrides.</summary>
    private static bool IsPubliclySettable(IPropertySymbol property)
    {
        for (var current = property; current is not null; current = current.OverriddenProperty)
        {
            if (current.SetMethod is { DeclaredAccessibility: Accessibility.Public })
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Why a property type can't carry a mapped value, or <see langword="null"/> if it can. C# only allows primitives,
    /// <see cref="string"/>, <see cref="object"/>, <see cref="Type"/>, enums and 1-D arrays of those as attribute
    /// arguments; FancyEnum doesn't support the arrays yet.
    /// </summary>
    private static string? UnsupportedMemberSetTypeReason(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
        {
            return "is an array, which FancyEnum doesn't support as a mapped value";
        }
        var supported = type.TypeKind == TypeKind.Enum || type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Type" || type.SpecialType is
            SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_String or SpecialType.System_Object;
        return supported ? null : "can't be an attribute argument, so no enum member could ever set it";
    }

    /// <summary>
    /// Reads an <c>object?</c>-typed FancyEnumMemberSetItem value (DefaultValue/NotMatched), reporting HENUM012 and
    /// returning <see langword="null"/> when it doesn't fit the property: it must be exactly the property's type (e.g.
    /// <c>5L</c>, not <c>5</c>, for a long), anything for an <see cref="object"/> property, and null only for a
    /// reference type.
    /// </summary>
    private static TypedConstant? ReadMatchingValue(AttributeData? item, string settingName, IPropertySymbol property, string shapeName, List<DiagnosticInfo> diagnostics)
    {
        if (!item.TryReadNamedTypedConstant(settingName, out var value))
        {
            return null;
        }
        var fits = value.IsNull
            ? property.Type.IsReferenceType
            : property.Type.SpecialType == SpecialType.System_Object || SymbolEqualityComparer.Default.Equals(value.Type, property.Type);
        if (fits)
        {
            return value;
        }
        diagnostics.Add(DiagnosticInfo.Create(EnumGeneratorDiagnostics.MemberSetValueTypeMismatch, property.Locations.FirstOrDefault(),
            shapeName, property.Name, property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), value.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "null", settingName));
        return null;
    }

    /// <summary>Whether a constructor parameter feeds a given property: explicitly via <see cref="FancyEnumConstructorMappingAttribute"/>, or by matching name (ordinal-ignore-case) and type.</summary>
    private static bool ParameterMapsToProperty(IParameterSymbol parameter, IPropertySymbol property, INamedTypeSymbol? constructorMappingAttributeType)
    {
        var mapping = parameter.GetAttributes().FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, constructorMappingAttributeType));
        if (mapping is not null)
        {
            return string.Equals(mapping.ConstructorArguments.FirstOrDefault().Value as string, property.Name, StringComparison.Ordinal);
        }
        return string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase) && SymbolEqualityComparer.Default.Equals(parameter.Type, property.Type);
    }

    /// <summary>Identifies a constructor overload by its parameter types, so an attribute application can find the shape's model of the constructor it bound to.</summary>
    private static string ConstructorSignature(IMethodSymbol constructor) =>
        string.Join(",", constructor.Parameters.Select(static parameter => parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
}
