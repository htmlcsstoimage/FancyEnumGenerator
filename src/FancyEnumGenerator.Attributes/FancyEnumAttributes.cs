namespace FancyEnumGenerator.Attributes;

/// <summary>
/// What a string field mapping yields for an enum member that has no explicit value for that field.
/// Used by <see cref="FancyEnumMemberMappingSettingsAttribute.NotDefined"/>.
/// </summary>
public enum FancyEnumMemberFallbackOption
{
    /// <summary>
    /// No fallback: the member is treated as unmapped for this field, so the property returns the field's
    /// "not matched" result (<see cref="FancyEnumMemberMappingSettingsAttribute.NotMatched"/>, <see langword="null"/>,
    /// an exception, or <see cref="string.Empty"/>), and the member can't be parsed from this field.
    /// </summary>
    Skip,

    /// <summary>The member's own name, exactly as declared (emitted as <c>nameof(...)</c>, so it tracks renames).</summary>
    NameOf,

    /// <summary>The member's name, lower-cased with the invariant culture.</summary>
    NameOfLower,

    /// <summary>The member's name, upper-cased with the invariant culture.</summary>
    NameOfUpper
}

/// <summary>
/// How the generated <c>ToStringFancy()</c> (and the default <c>TryParseFancy</c>, which also accepts these
/// strings) renders each member. Set via <see cref="FancyEnumAttribute.DefaultToStringBehavior"/>.
/// </summary>
public enum FancyEnumDefaultToStringBehavior
{
    /// <summary>The member's own name, exactly as declared. The default.</summary>
    NameOf,

    /// <summary>The member's name, lower-cased with the invariant culture.</summary>
    NameOfLower,

    /// <summary>The member's name, upper-cased with the invariant culture.</summary>
    NameOfUpper,

    /// <summary>
    /// The member's string mapping for the field named by <see cref="FancyEnumAttribute.DefaultToStringCustomField"/>.
    /// Every member must define that field; a member that doesn't is a build error (HENUM005).
    /// </summary>
    CustomFieldRequired,

    /// <summary>
    /// The member's string mapping for the field named by <see cref="FancyEnumAttribute.DefaultToStringCustomField"/>,
    /// falling back to the member's name for members that don't define it.
    /// </summary>
    CustomFieldFallback,

    /// <summary>Reads <see cref="System.ComponentModel.DescriptionAttribute"/>, falling back to the member's name when absent.</summary>
    DescriptionAttribute,

    /// <summary>
    /// Reads <c>System.ComponentModel.DataAnnotations.DisplayAttribute.Name</c> (<c>[Display(Name = "...")]</c>),
    /// falling back to the member's name when absent. Note this is not <see cref="System.ComponentModel.DisplayNameAttribute"/>,
    /// which doesn't allow <see cref="AttributeTargets.Field"/> and so can never be applied to an enum member.
    /// </summary>
    DisplayAttribute,

    /// <summary>
    /// Reads <c>System.Runtime.Serialization.EnumMemberAttribute.Value</c> (<c>[EnumMember(Value = "...")]</c>),
    /// falling back to the member's name when absent.
    /// </summary>
    EnumMemberAttribute,

    /// <summary>
    /// Reads <c>System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute</c> (<c>[JsonStringEnumMemberName("...")]</c>,
    /// .NET 9+), falling back to the member's name when absent.
    /// </summary>
    JsonStringEnumMemberNameAttribute
}

/// <summary>
/// Generates fast extension members for this enum: <c>ToStringFancy()</c>, <c>TryParseFancy</c>/<c>ParseOrUnknown</c>,
/// <c>FromUnderlying</c>, <c>IsUnknown</c>, <c>Values</c>, flags helpers, and a property per field mapping declared
/// with <see cref="FancyEnumMemberAttribute"/>/<see cref="FancyEnumMemberAttribute{T}"/>.
/// </summary>
/// <remarks>
/// By default the enum must declare a member named <c>Unknown</c> (case-insensitive) with value <c>0</c>, and its
/// values must be contiguous; see <see cref="AllowNoUnknown"/> and <see cref="AllowNonContiguous"/>.
/// <para>
/// Every property here can also be defaulted assembly-wide with <see cref="FancyEnumDefaultsAttribute"/>, or
/// repo-wide with an MSBuild property named <c>FancyEnum</c> + the property name (e.g. <c>FancyEnumAllowNoUnknown</c>).
/// Precedence, highest first: a value set explicitly here, the assembly attribute, the MSBuild property, the
/// library default.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class FancyEnumAttribute : Attribute
{
    /// <summary>
    /// Don't generate the .NET 8+ <c>[InlineArray]</c> struct. <c>Values</c>/<c>AsSpan</c> are then only generated
    /// when <see cref="CreateStaticReadonlyCollection"/> is also set (backed by a plain array), and flags enums lose
    /// <c>ListFlagMembers</c>. Default: <see langword="false"/>.
    /// </summary>
    public bool NoInlineArray { get; set; }

    /// <summary>
    /// Cache the member collection in a static field, built lazily on first access, and also generate <c>AsSpan</c>
    /// (a zero-copy <c>ReadOnlySpan&lt;T&gt;</c> over it). Default: <see langword="false"/>, in which case
    /// on .NET 8+ <c>Values</c> is built fresh on every access (a value-type copy, no heap allocation) and there is
    /// no <c>AsSpan</c>; on older targets neither is generated.
    /// </summary>
    /// <remarks>
    /// When the collection is a plain array (older targets, or <see cref="NoInlineArray"/>), <c>Values</c> is typed
    /// <see cref="System.Collections.Generic.IReadOnlyList{T}"/> so callers can't mutate the shared storage.
    /// </remarks>
    public bool CreateStaticReadonlyCollection { get; set; }

    /// <summary>
    /// Allow gaps between the enum's numeric values. Without this, non-contiguous values are a build error (HENUM002).
    /// Contiguous enums get range-check fast paths in <c>FromUnderlying</c>/<c>IsUnknown</c>; others use a switch.
    /// Default: <see langword="false"/>, except <see langword="true"/> for <see cref="System.FlagsAttribute"/> enums,
    /// whose single-bit values are never contiguous.
    /// </summary>
    public bool AllowNonContiguous { get; set; }

    /// <summary>
    /// Don't require an <c>Unknown = 0</c> member. Without this, a missing one is a build error (HENUM001). When there
    /// is no Unknown member, <c>IsUnknown</c> isn't generated and parsing falls back to the first member via
    /// <c>ParseOrDefault</c> instead of <c>ParseOrUnknown</c>. Default: <see langword="false"/>.
    /// </summary>
    public bool AllowNoUnknown { get; set; }

    /// <summary>
    /// Keep members marked <see cref="System.ObsoleteAttribute"/>. By default they are excluded from all generated code.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool IncludeObsolete { get; set; }

    /// <summary>How <c>ToStringFancy()</c> renders each member. Default: <see cref="FancyEnumDefaultToStringBehavior.NameOf"/>.</summary>
    public FancyEnumDefaultToStringBehavior DefaultToStringBehavior { get; set; }

    /// <summary>
    /// The string field read by <see cref="FancyEnumDefaultToStringBehavior.CustomFieldRequired"/> and
    /// <see cref="FancyEnumDefaultToStringBehavior.CustomFieldFallback"/>. Ignored by every other behavior.
    /// </summary>
    public string? DefaultToStringCustomField { get; set; }

    /// <summary>
    /// Also generate <c>TryFormat(Span&lt;char&gt; destination, out int charsWritten)</c>, writing the
    /// <c>ToStringFancy()</c> value without allocating. Default: <see langword="false"/>.
    /// </summary>
    public bool CreateTryFormat { get; set; }

    /// <summary>
    /// Also generate UTF-8 (<c>ReadOnlySpan&lt;byte&gt;</c>) overloads of
    /// <c>TryParseFancy</c>, <c>ParseOrUnknown</c>/<c>ParseOrDefault</c> and any <c>TryParseFrom_*</c> parsers.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool CreateByteParsing { get; set; }

    /// <summary>
    /// Also generate <c>IsValidPrefixFancy(ReadOnlySpan&lt;byte&gt; prefix, ReadOnlySpan&lt;byte&gt; next)</c>:
    /// whether the UTF-8 bytes seen so far could still become a parseable member, for incremental/streaming decoders
    /// that want to give up on an unknown name early instead of buffering all of it. Niche, so off by default; it
    /// checks every token in turn, so its cost grows with the enum's size. See docs/is-valid-prefix.md.
    /// Independent of <see cref="CreateByteParsing"/>. Default: <see langword="false"/>.
    /// </summary>
    public bool CreateIsValidPrefix { get; set; }

    /// <summary>
    /// Whether the overloads of <c>TryParseFancy</c>/<c>ParseOrUnknown</c> without an <c>ignoreCase</c> parameter
    /// compare case-sensitively. Also the default for this enum's field parsers (<c>TryParseFrom_*</c>) that don't set
    /// their own. Default: <see langword="true"/>.
    /// </summary>
    public bool ParseCaseSensitive { get; set; } = true;
}

/// <summary>
/// Assembly-wide defaults for every FancyEnum-generated enum in this compilation:
/// <c>[assembly: FancyEnumDefaults(AllowNoUnknown = true)]</c>. Apply at most once per assembly.
/// </summary>
/// <remarks>
/// Only properties you actually set here take effect; unset ones fall through to the matching MSBuild property
/// (<c>FancyEnum</c> + the property name, e.g. <c>FancyEnumAllowNoUnknown</c>) and then the library default. A
/// value set explicitly on a specific enum's <see cref="FancyEnumAttribute"/> always wins. See the like-named
/// properties on <see cref="FancyEnumAttribute"/> for what each one does.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class FancyEnumDefaultsAttribute : Attribute
{
    /// <summary>Default for <see cref="FancyEnumAttribute.NoInlineArray"/>.</summary>
    public bool NoInlineArray { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.CreateStaticReadonlyCollection"/>.</summary>
    public bool CreateStaticReadonlyCollection { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.AllowNonContiguous"/>.</summary>
    public bool AllowNonContiguous { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.AllowNoUnknown"/>.</summary>
    public bool AllowNoUnknown { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.IncludeObsolete"/>.</summary>
    public bool IncludeObsolete { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.DefaultToStringBehavior"/>.</summary>
    public FancyEnumDefaultToStringBehavior DefaultToStringBehavior { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.DefaultToStringCustomField"/>.</summary>
    public string? DefaultToStringCustomField { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.CreateTryFormat"/>.</summary>
    public bool CreateTryFormat { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.CreateByteParsing"/>.</summary>
    public bool CreateByteParsing { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.CreateIsValidPrefix"/>.</summary>
    public bool CreateIsValidPrefix { get; set; }

    /// <summary>Default for <see cref="FancyEnumAttribute.ParseCaseSensitive"/> (library default <see langword="true"/>).</summary>
    public bool ParseCaseSensitive { get; set; }

    /// <summary>
    /// Whether to generate any parsing code at all: <c>TryParseFancy</c>, <c>ParseOrUnknown</c>/<c>ParseOrDefault</c>,
    /// <c>IsValidPrefixFancy</c> and every <c>TryParseFrom_*</c>. Set <see langword="false"/> to drop it all when you
    /// only need formatting. Library default: <see langword="true"/>. Assembly/MSBuild only - no per-enum equivalent.
    /// </summary>
    public bool GenerateParseMethods { get; set; }

    /// <summary>
    /// Whether generated files are named <c>*.FancyEnum.g.cs</c> (the library default, <see langword="true"/>) or
    /// <c>*.FancyEnum.cs</c>. Many tools treat <c>.g.cs</c> as generated and skip formatting/linting it.
    /// Assembly/MSBuild only - no per-enum equivalent.
    /// </summary>
    public bool UseGeneratedFileSuffix { get; set; }
}

/// <summary>
/// Configures the generated property for a string field mapping (declared with <see cref="FancyEnumMemberAttribute"/>),
/// or declares a string field that no member maps explicitly. Apply to the enum, once per field.
/// For fields of another type, use <see cref="FancyEnumMemberMappingSettingsAttribute{T}"/>.
/// </summary>
/// <param name="fieldName">The field name, matching the one used in <see cref="FancyEnumMemberAttribute"/>. Also the generated property's name.</param>
/// <remarks>
/// A member "matches" a field if it maps it explicitly, or gets a value from <see cref="NotDefined"/>. Anything
/// else - including runtime values that aren't a declared member - gets the not-matched result: in priority order,
/// an exception (<see cref="ThrowOnNotMatched"/>), <see cref="NotMatched"/>, <see langword="null"/>
/// (<see cref="ReturnNullOnNotMatched"/>), or <see cref="string.Empty"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = true)]
public sealed class FancyEnumMemberMappingSettingsAttribute(string fieldName) : Attribute
{
    /// <summary>The field this configures.</summary>
    public string FieldName { get; } = fieldName;

    /// <summary>
    /// The value for members that don't map this field explicitly. Default: <see cref="FancyEnumMemberFallbackOption.Skip"/>
    /// (they get the not-matched result instead).
    /// </summary>
    public FancyEnumMemberFallbackOption NotDefined { get; set; }

    /// <summary>The value returned when no mapping applies. Overridden by <see cref="ThrowOnNotMatched"/>.</summary>
    public string? NotMatched { get; set; }

    /// <summary>
    /// Type the generated property as <c>string?</c> and return <see langword="null"/> when no mapping applies
    /// (unless <see cref="NotMatched"/> or <see cref="ThrowOnNotMatched"/> is also set, which take precedence).
    /// </summary>
    public bool ReturnNullOnNotMatched { get; set; }

    /// <summary>Throw <see cref="System.ArgumentOutOfRangeException"/> when no mapping applies, instead of returning a fallback.</summary>
    public bool ThrowOnNotMatched { get; set; }

    /// <summary>
    /// Also generate <c>TryParseFrom_{FieldName}</c>, parsing this field's values (including <see cref="NotDefined"/>
    /// fallbacks) back to the enum member.
    /// </summary>
    public bool ParseFrom { get; set; }

    /// <summary>
    /// Whether the <c>TryParseFrom_{FieldName}</c> overload without an <c>ignoreCase</c> parameter compares
    /// case-sensitively. When not set, inherits the enum's <see cref="FancyEnumAttribute.ParseCaseSensitive"/>
    /// (itself defaulted assembly-wide or via MSBuild like any other option).
    /// </summary>
    public bool ParseCaseSensitive { get; set; }

    /// <summary>
    /// Also generate <c>TryFormat_{FieldName}(Span&lt;char&gt; destination, out int charsWritten)</c> and a
    /// <c>{FieldName}_LongestCharLength</c> constant for sizing its buffer.
    /// </summary>
    public bool CreateTryFormat { get; set; }

    /// <summary>
    /// Also generate a <c>{FieldName}Bytes</c> property returning the value as a UTF-8 <c>u8</c> literal
    /// (<c>ReadOnlySpan&lt;byte&gt;</c>). Requires every value to be a compile-time constant.
    /// </summary>
    public bool IncludeUtf8Value { get; set; }
}

/// <summary>
/// Configures the generated property for a field mapping of type <typeparamref name="T"/> (declared with
/// <see cref="FancyEnumMemberAttribute{T}"/>), or declares such a field. Apply to the enum, once per field.
/// </summary>
/// <typeparam name="T">The field's type. Must be an attribute-legal type: a primitive, <see cref="string"/>, an enum, or <see cref="System.Type"/>.</typeparam>
/// <param name="fieldName">The field name, matching the one used in <see cref="FancyEnumMemberAttribute{T}"/>. Also the generated property's name.</param>
/// <remarks>
/// See <see cref="FancyEnumMemberMappingSettingsAttribute"/> for how matching and the not-matched result work; the
/// only difference is that there <see cref="NotDefined"/> and <see cref="NotMatched"/> are literal values of
/// <typeparamref name="T"/>, and the final fallback is <c>default(T)</c>. <see cref="ParseFrom"/>,
/// <see cref="CreateTryFormat"/> and <see cref="IncludeUtf8Value"/> only apply when <typeparamref name="T"/> is
/// <see cref="string"/>; on other types they're reported as unavailable (HENUM009, HENUM008, HENUM011).
/// </remarks>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = true)]
public sealed class FancyEnumMemberMappingSettingsAttribute<T>(string fieldName) : Attribute
{
    /// <summary>The field this configures.</summary>
    public string FieldName { get; } = fieldName;

    /// <summary>The value for members that don't map this field explicitly. When unset, they get the not-matched result.</summary>
    public T? NotDefined { get; set; }

    /// <summary>The value returned when no mapping applies. Overridden by <see cref="ThrowOnNotMatched"/>.</summary>
    public T? NotMatched { get; set; }

    /// <summary>
    /// Type the generated property as nullable and return <see langword="null"/> when no mapping applies (unless
    /// <see cref="NotMatched"/> or <see cref="ThrowOnNotMatched"/> is also set, which take precedence).
    /// </summary>
    public bool ReturnNullOnNotMatched { get; set; }

    /// <summary>Throw <see cref="System.ArgumentOutOfRangeException"/> when no mapping applies, instead of returning a fallback.</summary>
    public bool ThrowOnNotMatched { get; set; }

    /// <summary>String fields only: also generate <c>TryFormat_{FieldName}</c> and <c>{FieldName}_LongestCharLength</c>.</summary>
    public bool CreateTryFormat { get; set; }

    /// <summary>String fields only: also generate <c>TryParseFrom_{FieldName}</c>, parsing this field's values back to the member.</summary>
    public bool ParseFrom { get; set; }

    /// <summary>
    /// Whether the <c>TryParseFrom_{FieldName}</c> overload without an <c>ignoreCase</c> parameter compares
    /// case-sensitively. When not set, inherits the enum's <see cref="FancyEnumAttribute.ParseCaseSensitive"/>
    /// (itself defaulted assembly-wide or via MSBuild like any other option).
    /// </summary>
    public bool ParseCaseSensitive { get; set; }

    /// <summary>String fields only: also generate a UTF-8 <c>{FieldName}Bytes</c> property.</summary>
    public bool IncludeUtf8Value { get; set; }
}

/// <summary>
/// Marks your own attribute class as a "member set": applying it once to an enum member maps each of its public
/// instance properties to its own generated field (named after the property, or
/// <see cref="FancyEnumMemberSetItemAttribute.Name"/>), except those marked <see cref="FancyEnumMemberSetItemAttribute.Ignore"/>.
/// Inherited public properties count too. The enum itself still needs <see cref="FancyEnumAttribute"/>; using a
/// member set on an enum without it is reported (HENUM016) since nothing would be generated.
/// </summary>
/// <remarks>
/// <code>
/// [FancyEnumMemberSet]
/// public sealed class FruitMetadataAttribute : Attribute
/// {
///     public string? Label { get; set; }
///     public int Order { get; set; }
/// }
///
/// [FancyEnum]
/// public enum Fruit { Unknown = 0, [FruitMetadata(Label = "apple", Order = 1)] Apple = 1 }
/// // generates Fruit.Apple.Label and Fruit.Apple.Order
/// </code>
/// <para>
/// A settable property's value comes from a named argument. A read-only property's value can only come from a
/// constructor parameter: matched automatically when the parameter's name (case-insensitively) and type match the
/// property, otherwise the parameter must carry <see cref="FancyEnumConstructorMappingAttribute"/>. Every public
/// constructor with parameters is validated this way (HENUM013), whether or not any enum uses that overload.
/// A named argument wins over a constructor argument for the same property.
/// </para>
/// <para>
/// Only the raw argument as written is ever read - the constructor never runs at compile time, so any
/// transformation its body applies (e.g. <c>Order = order + 1;</c>) is invisible to the generator. Keep member-set
/// constructors to plain assignments, or document the difference for your users.
/// </para>
/// <para>
/// Every included property must be settable by some enum member, or it's an error (HENUM015): its type must be one C#
/// allows as an attribute argument (a primitive, <see cref="string"/>, <see cref="object"/>, <see cref="System.Type"/>
/// or an enum - so no <c>int?</c>, <c>DateTime</c> or arrays), and a read-only property needs a constructor parameter
/// mapped to it. Exclude anything else with <see cref="FancyEnumMemberSetItemAttribute.Ignore"/>. A member that
/// doesn't set a property - or doesn't use the attribute at all - falls back per that field's settings, e.g.
/// <see cref="FancyEnumMemberSetItemAttribute.DefaultValue"/>.
/// </para>
/// <para>
/// Each member-set class is validated once, where it's declared, whether or not any enum uses it yet.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FancyEnumMemberSetAttribute : Attribute
{
}

/// <summary>
/// Applied to a constructor parameter of a <see cref="FancyEnumMemberSetAttribute"/>-marked class to say which property
/// it feeds, when the parameter's name and type don't already match that property automatically.
/// </summary>
/// <param name="propertyName">The property's name. Prefer <c>nameof(YourProperty)</c>.</param>
/// <remarks>
/// <code>
/// public CssClassAttribute([FancyEnumConstructorMapping(nameof(ClassName))] string css) => ClassName = css;
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FancyEnumConstructorMappingAttribute(string propertyName) : Attribute
{
    /// <summary>The property this parameter's value is mapped to.</summary>
    public string PropertyName { get; } = propertyName;
}

/// <summary>
/// Per-property settings for a property of a <see cref="FancyEnumMemberSetAttribute"/>-marked class: its generated field
/// name, whether it's a field at all, and the same fallback/parse/format options as
/// <see cref="FancyEnumMemberMappingSettingsAttribute"/>.
/// </summary>
/// <remarks>
/// These are the defaults every enum using the member set inherits. An enum can override them for its own copy of a
/// field with <see cref="FancyEnumMemberMappingSettingsAttribute"/>/<see cref="FancyEnumMemberMappingSettingsAttribute{T}"/>
/// for the same field name, which always takes precedence.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FancyEnumMemberSetItemAttribute : Attribute
{
    /// <summary>The generated field (and property) name. Defaults to the property's own name.</summary>
    public string? Name { get; set; }

    /// <summary>Don't treat this property as a field at all - no generated member, no validation.</summary>
    public bool Ignore { get; set; }

    /// <summary>
    /// The value for members that don't set this property. Must be a constant of exactly the property's type (e.g.
    /// <c>5L</c>, not <c>5</c>, for a <see cref="long"/>); a mismatch is reported (HENUM012) and the default is ignored.
    /// <see langword="null"/> is only valid for reference-typed properties.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// The value returned when no mapping applies (including for values that aren't a declared member). Overridden by
    /// <see cref="ThrowOnNotMatched"/>. Like <see cref="DefaultValue"/>, it must be a constant of exactly the property's
    /// type; a mismatch is reported (HENUM012) and the value is ignored.
    /// </summary>
    public object? NotMatched { get; set; }

    /// <summary>Type the generated property as nullable and return <see langword="null"/> when no mapping applies.</summary>
    public bool ReturnNullOnNotMatched { get; set; }

    /// <summary>Throw <see cref="System.ArgumentOutOfRangeException"/> when no mapping applies, instead of returning a fallback.</summary>
    public bool ThrowOnNotMatched { get; set; }

    /// <summary>String properties only: also generate <c>TryParseFrom_{Name}</c>.</summary>
    public bool ParseFrom { get; set; }

    /// <summary>
    /// Whether the <c>TryParseFrom_{Name}</c> overload without an <c>ignoreCase</c> parameter is case-sensitive. When not
    /// set, inherits the using enum's <see cref="FancyEnumAttribute.ParseCaseSensitive"/>.
    /// </summary>
    public bool ParseCaseSensitive { get; set; }

    /// <summary>String properties only: also generate <c>TryFormat_{Name}</c> and <c>{Name}_LongestCharLength</c>.</summary>
    public bool CreateTryFormat { get; set; }

    /// <summary>String properties only: also generate a UTF-8 <c>{Name}Bytes</c> property.</summary>
    public bool IncludeUtf8Value { get; set; }
}

/// <summary>Per-member settings for an enum member.</summary>
/// <remarks>Not sealed: an attribute deriving from this one is honored too.</remarks>
[AttributeUsage(AttributeTargets.Field)]
public class FancyEnumMemberSettingsAttribute : Attribute
{
    /// <summary>
    /// Excludes this member from the generated <c>Values</c>/<c>AsSpan</c> collection (and <c>ListFlagMembers</c>).
    /// On a <c>[Flags]</c> enum this also suppresses the "not a single bit" warning (HENUM010), so use it on composite
    /// members like <c>All = Read | Write | Execute</c>. The member still gets <c>ToStringFancy()</c>, parsing, and
    /// its field mappings.
    /// </summary>
    public bool ExcludeFromValues { get; set; }
}

/// <summary>
/// Maps an enum member to a value of type <typeparamref name="T"/> for the field <c>fieldName</c>, generating a
/// property of that name on the enum: <c>[FancyEnumMember&lt;int&gt;("SortOrder", 1)]</c> gives <c>Fruit.Apple.SortOrder</c>.
/// For string values, use the non-generic <see cref="FancyEnumMemberAttribute"/> shorthand.
/// </summary>
/// <typeparam name="T">The field's type. Must be an attribute-legal type: a primitive, <see cref="string"/>, an enum, or <see cref="System.Type"/>.</typeparam>
/// <remarks>
/// Every member mapping the same field must use the same <typeparamref name="T"/> (HENUM003), and a member may map
/// a given field only once. Configure what unmapped members get with <see cref="FancyEnumMemberMappingSettingsAttribute{T}"/>.
/// Deriving from this attribute isn't supported - to define your own metadata attribute, use <see cref="FancyEnumMemberSetAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class FancyEnumMemberAttribute<T> : Attribute
{
    /// <summary>Maps the field without a literal value - set <see cref="Value"/> or <see cref="StaticMethodName"/>/<see cref="StaticMethodSource"/>.</summary>
    /// <param name="fieldName">The field (and generated property) name.</param>
    public FancyEnumMemberAttribute(string fieldName)
    {
        FieldName = fieldName;
    }

    /// <summary>Maps the field to <paramref name="value"/>.</summary>
    /// <param name="fieldName">The field (and generated property) name.</param>
    /// <param name="value">The member's value for this field.</param>
    public FancyEnumMemberAttribute(string fieldName, T? value)
    {
        FieldName = fieldName;
        Value = value;
    }

    /// <summary>The field (and generated property) name.</summary>
    public string FieldName { get; }

    /// <summary>The member's value for this field.</summary>
    public T? Value { get; set; }

    /// <summary>
    /// The name of a parameterless static method, or static readonly field, on <see cref="StaticMethodSource"/> that
    /// supplies the value at runtime - for values that can't be attribute constants. Must be set together with
    /// <see cref="StaticMethodSource"/>, and takes precedence over <see cref="Value"/>. A field using this can't be
    /// parsed from, formatted with <c>TryFormat_*</c>, or exposed as UTF-8.
    /// </summary>
    public string? StaticMethodName { get; set; }

    /// <summary>The type declaring the member named by <see cref="StaticMethodName"/>.</summary>
    public Type? StaticMethodSource { get; set; }
}

/// <summary>
/// Maps an enum member to a string value for the field <c>fieldName</c>, generating a string property of that name
/// on the enum: <c>[FancyEnumMember("Label", "apple")]</c> gives <c>Fruit.Apple.Label</c>. Shorthand for
/// <see cref="FancyEnumMemberAttribute{T}"/> with <c>T</c> = <see cref="string"/>.
/// </summary>
/// <param name="fieldName">The field (and generated property) name.</param>
/// <param name="value">The member's value for this field.</param>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class FancyEnumMemberAttribute(string fieldName, string value) : FancyEnumMemberAttribute<string>(fieldName, value);
