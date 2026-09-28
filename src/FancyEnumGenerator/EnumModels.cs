namespace FancyEnumGenerator;

internal sealed record EnumModel
{
    public string Namespace { get; init; } = "";
    public string Name { get; init; } = "";
    public string GeneratedName { get; init; } = "";
    public string HintName { get; init; } = "";
    public string FullyQualifiedName { get; init; } = "";
    public string UnderlyingType { get; init; } = "int";
    public string Accessibility { get; init; } = "internal";
    public bool NoInlineArray { get; init; }
    public bool CreateStaticReadonlyCollection { get; init; }
    public bool AllowNonContiguous { get; init; }
    public bool AllowNoUnknown { get; init; }
    public bool IncludeObsolete { get; init; }
    public FancyEnumDefaultToStringBehavior DefaultToStringBehavior { get; init; }
    public string? DefaultToStringCustomField { get; init; }
    public bool CreateTryFormat { get; init; }
    public bool CreateByteParsing { get; init; }
    public bool CreateIsValidPrefix { get; init; }
    public bool ParseCaseSensitive { get; init; } = true;
    public bool GenerateParseMethods { get; init; } = true;
    public bool UseGeneratedFileSuffix { get; init; } = true;
    public bool IsFlags { get; init; }
    public EquatableArray<EnumMemberModel> Members { get; init; }
    public EquatableArray<MappingSettingsModel> MappingSettings { get; init; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}

internal sealed record EnumMemberModel
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? DisplayName { get; init; }
    public string? EnumMemberValue { get; init; }
    public string? JsonStringEnumMemberName { get; init; }
    public decimal NumericValue { get; init; }
    public bool ExcludeFromValues { get; init; }
    public EquatableArray<MemberMappingModel> Mappings { get; init; }
    public GeneratorLocationInfo? Location { get; init; }
}

internal sealed record MappingSettingsModel
{
    public string FieldName { get; init; } = "";
    public GeneratorLocationInfo? Location { get; init; }
    public string? ReturnType { get; init; }
    /// <summary>Whether <see cref="ReturnType"/> is a reference type other than string, whose "not matched" fallback is therefore null.</summary>
    public bool IsNonStringReferenceType { get; init; }
    public FancyEnumMemberFallbackOption NotDefined { get; init; }
    public string? NotDefinedExpression { get; init; }
    public string? NotMatchedExpression { get; init; }
    public string? NotDefinedStringValue { get; init; }
    public string? NotMatchedStringValue { get; init; }
    public bool ReturnNullOnNotMatched { get; init; }
    public bool ThrowOnNotMatched { get; init; }
    public bool CreateTryFormat { get; init; }
    public bool IncludeUtf8Value { get; init; }
    public bool ParseFrom { get; init; }
    /// <summary>Explicitly set for this field, or null to inherit the enum's resolved <see cref="EnumModel.ParseCaseSensitive"/>.</summary>
    public bool? ParseCaseSensitive { get; init; }
}

/// <summary>
/// A <see cref="FancyEnumMemberSetAttribute"/>-marked attribute class, analyzed once (not once per enum using it):
/// its mapped fields, how each public constructor's parameters feed them, and any problems with the class itself.
/// </summary>
internal sealed record MemberSetShapeModel
{
    /// <summary>The attribute class, fully qualified: the key enums look shapes up by.</summary>
    public string FullyQualifiedName { get; init; } = "";
    public EquatableArray<MemberSetFieldModel> Fields { get; init; }
    public EquatableArray<MemberSetConstructorModel> Constructors { get; init; }
    public EquatableArray<DiagnosticInfo> Diagnostics { get; init; }
}

internal sealed record MemberSetFieldModel
{
    public string PropertyName { get; init; } = "";
    public string FieldName { get; init; } = "";
    public string ReturnType { get; init; } = "";
    /// <summary>The shape-level fallback settings from the property's FancyEnumMemberSetItem, if any.</summary>
    public MappingSettingsModel Settings { get; init; } = new();
}

internal sealed record MemberSetConstructorModel
{
    /// <summary>The constructor's parameter types, fully qualified and comma-joined: identifies which overload an attribute application bound to.</summary>
    public string Signature { get; init; } = "";
    /// <summary>For each parameter, in order, the name of the property it feeds, or "" if it resolves to none (reported as HENUM013).</summary>
    public EquatableArray<string> ParameterProperties { get; init; }
}

internal sealed record MemberMappingModel
{
    public string FieldName { get; init; } = "";
    public string ReturnType { get; init; } = "string";
    /// <summary>Whether <see cref="ReturnType"/> is a reference type other than string, whose "not matched" fallback is therefore null.</summary>
    public bool IsNonStringReferenceType { get; init; }
    public string Expression { get; init; } = "";
    public string? StringValue { get; init; }
    public bool HasStaticMemberSource { get; init; }
}
