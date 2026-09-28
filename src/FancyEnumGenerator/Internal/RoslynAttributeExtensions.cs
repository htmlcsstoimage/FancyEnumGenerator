using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FancyEnumGenerator.Internal;

internal static class RoslynAttributeExtensions
{
    public static bool TryReadConstructorArgument(this AttributeData? attributeData, int index, out TypedConstant value)
    {
        if (attributeData is not null && index >= 0 && index < attributeData.ConstructorArguments.Length)
        {
            value = attributeData.ConstructorArguments[index];
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryReadConstructorInt(this AttributeData? attributeData, int index, out int value)
    {
        value = default;
        if (!attributeData.TryReadConstructorArgument(index, out var argument) || argument.Value is not int intValue)
        {
            return false;
        }

        value = intValue;
        return true;
    }

    public static bool TryReadConstructorEnum<TEnum>(this AttributeData? attributeData, int index, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return attributeData.TryReadConstructorArgument(index, out var argument) && argument.TryReadEnum(out value);
    }

    public static bool TryReadConstructorType(this AttributeData? attributeData, int index, out ITypeSymbol? value)
    {
        value = null;
        if (!attributeData.TryReadConstructorArgument(index, out var argument) || argument.Kind != TypedConstantKind.Type)
        {
            return false;
        }

        value = argument.Value as ITypeSymbol;
        return true;
    }

    /// <summary>
    /// Reads the conventional friendly name from constructor argument zero or, when absent, the named <c>FriendlyName</c> argument.
    /// </summary>
    /// <param name="explicitlySpecified">
    /// <see langword="true"/> when either argument form was supplied, including when the named argument's value is <see langword="null"/>.
    /// </param>
    public static string? TryReadFriendlyName(this AttributeData? attributeData, out bool explicitlySpecified)
    {
        explicitlySpecified = false;
        if (attributeData is null)
        {
            return null;
        }

        if (attributeData.ConstructorArguments.Length > 0 &&
            attributeData.ConstructorArguments[0].Value is string ctorFriendly)
        {
            explicitlySpecified = true;
            return ctorFriendly;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key == "FriendlyName")
            {
                explicitlySpecified = true;
                return pair.Value.Value as string;
            }
        }

        return null;
    }

    public static string? TryReadNamedString(this AttributeData? attributeData, string name)
    {
        if (attributeData is null)
        {
            return null;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key == name)
            {
                return pair.Value.Value as string;
            }
        }

        return null;
    }

    public static bool TryReadNamedString(this AttributeData? attributeData, string name, out string? value)
    {
        value = null;
        if (!attributeData.TryReadNamedTypedConstant(name, out var constant) || constant.Kind != TypedConstantKind.Primitive)
        {
            return false;
        }

        value = constant.Value as string;
        return constant.IsNull || constant.Value is string;
    }

    public static bool TryReadNamedType(this AttributeData? attributeData, string name, out ITypeSymbol? value)
    {
        value = null;
        if (!attributeData.TryReadNamedTypedConstant(name, out var constant) || constant.Kind != TypedConstantKind.Type)
        {
            return false;
        }

        value = constant.Value as ITypeSymbol;
        return true;
    }

    public static bool TryReadNamedTypedConstant(this AttributeData? attributeData, string name, out TypedConstant value)
    {
        if (attributeData is not null)
        {
            foreach (var pair in attributeData.NamedArguments)
            {
                if (pair.Key == name)
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    public static int TryReadNamedInt(this AttributeData attributeData, string name)
    {
        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key == name && pair.Value.Value is int intValue)
            {
                return intValue;
            }
        }

        return 0;
    }

    public static bool TryReadNamedInt(this AttributeData attributeData, string name, [NotNullWhen(true)] out int? value)
    {
        value = null;
        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key == name && pair.Value.Value is int intValue)
            {
                value = intValue;
                return true;
            }
        }

        return false;
    }

    public static bool TryReadNamedBool(this AttributeData? attributeData, string name)
    {
        if (attributeData is null)
        {
            return false;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key == name && pair.Value.Value is bool boolValue)
            {
                return boolValue;
            }
        }

        return false;
    }

    public static bool TryReadNamedBool(this AttributeData? attributeData, string name, bool defaultValue) =>
        attributeData.TryReadNamedBool(name, out var value) ? value.Value : defaultValue;

    public static bool TryReadNamedBool(this AttributeData? attributeData, string name, [NotNullWhen(true)] out bool? value)
    {
        value = null;
        if (attributeData is null)
        {
            return false;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key == name && pair.Value.Value is bool boolValue)
            {
                value = boolValue;
                return true;
            }
        }

        return false;
    }

    public static bool TryReadNamedStringArray(this AttributeData? attributeData, string name, out ImmutableArray<string> result)
    {
        if (attributeData is null)
        {
            result = [];
            return false;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != name)
            {
                continue;
            }

            if (pair.Value.Kind == TypedConstantKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<string>(pair.Value.Values.Length);
                foreach (var item in pair.Value.Values)
                {
                    if (item.Value is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        builder.Add(value);
                    }
                }

                result = builder.ToImmutable();
                return true;
            }

            if (pair.Value.Value is string singleValue && !string.IsNullOrWhiteSpace(singleValue))
            {
                result = [singleValue];
                return true;
            }
        }

        result = [];
        return false;
    }

    public static bool TryReadNamedTypeArray(this AttributeData? attributeData, string name, out ImmutableArray<string> result)
    {
        if (attributeData is null)
        {
            result = [];
            return false;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != name)
            {
                continue;
            }

            if (pair.Value.Kind == TypedConstantKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<string>(pair.Value.Values.Length);
                foreach (var item in pair.Value.Values)
                {
                    if (item.Value is ITypeSymbol typeSymbol)
                    {
                        builder.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    }
                }

                result = builder.ToImmutable();
                return true;
            }

            if (pair.Value.Value is ITypeSymbol singleType)
            {
                result = [singleType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)];
                return true;
            }
        }

        result = [];
        return false;
    }

    public static double? TryReadNamedDouble(this AttributeData? attributeData, string name)
    {
        if (attributeData is null)
        {
            return null;
        }

        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != name)
            {
                continue;
            }

            return pair.Value.Value switch
            {
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                decimal decimalValue => (double)decimalValue,
                int intValue => intValue,
                long longValue => longValue,
                _ => null
            };
        }

        return null;
    }

    public static string? TryReadEnumMemberName(this AttributeData attributeData, string name)
    {
        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != name)
            {
                continue;
            }

            if (pair.Value.Type is not INamedTypeSymbol enumType)
            {
                return null;
            }

            var raw = pair.Value.TryGetInt64();
            if (raw is null)
            {
                return null;
            }

            foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.HasConstantValue && field.TryGetInt64() == raw)
                {
                    return field.Name;
                }
            }

            return null;
        }

        return null;
    }

    public static bool TryReadEnumMemberRawValue(this AttributeData attributeData, string name, [NotNullWhen(true)] out long? rawValue)
    {
        rawValue = null;
        foreach (var pair in attributeData.NamedArguments)
        {
            if (pair.Key != name)
            {
                continue;
            }

            if (pair.Value.Kind != TypedConstantKind.Enum)
            {
                return false;
            }

            rawValue = pair.Value.TryGetInt64();
            return rawValue.HasValue;
        }

        return false;
    }

    public static bool TryReadNamedEnum<TEnum>(this AttributeData? attributeData, string name, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return attributeData.TryReadNamedTypedConstant(name, out var constant) && constant.TryReadEnum(out value);
    }

    public static bool TryReadNamedEnumArray<TEnum>(this AttributeData? attributeData, string name, out ImmutableArray<TEnum> values, out bool isNull)
        where TEnum : struct, Enum
    {
        values = [];
        isNull = false;
        if (!attributeData.TryReadNamedTypedConstant(name, out var constant) || constant.Kind != TypedConstantKind.Array)
        {
            return false;
        }

        if (constant.IsNull)
        {
            isNull = true;
            return true;
        }

        var builder = ImmutableArray.CreateBuilder<TEnum>(constant.Values.Length);
        foreach (var item in constant.Values)
        {
            if (!item.TryReadEnum<TEnum>(out var value))
            {
                values = [];
                return false;
            }

            builder.Add(value);
        }

        values = builder.ToImmutable();
        return true;
    }

    public static bool TryReadEnum<TEnum>(this TypedConstant constant, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (constant.Kind != TypedConstantKind.Enum || constant.Value is null)
        {
            return false;
        }

        try
        {
            value = (TEnum)Enum.ToObject(typeof(TEnum), constant.Value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static GeneratorLocationInfo? GetAttributeLocation(this AttributeData? attributeData, CancellationToken cancellationToken = default)
    {
        return GeneratorLocationInfo.FromLocation(attributeData?.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation());
    }

    public static GeneratorLocationInfo? GetConstructorArgumentLocation(this AttributeData? attributeData, int index, CancellationToken cancellationToken = default)
    {
        if (attributeData?.AttributeConstructor is not { } constructor ||
            attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax syntax ||
            syntax.ArgumentList is null ||
            index < 0 ||
            index >= constructor.Parameters.Length)
        {
            return null;
        }

        var assignedParameters = new bool[constructor.Parameters.Length];
        var nextPositionalIndex = 0;
        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (argument.NameEquals is not null)
            {
                continue;
            }

            int parameterIndex;
            if (argument.NameColon is { } nameColon)
            {
                parameterIndex = -1;
                for (var i = 0; i < constructor.Parameters.Length; i++)
                {
                    if (string.Equals(constructor.Parameters[i].Name, nameColon.Name.Identifier.ValueText, StringComparison.Ordinal))
                    {
                        parameterIndex = i;
                        break;
                    }
                }

                if (parameterIndex < 0)
                {
                    continue;
                }
            }
            else
            {
                while (nextPositionalIndex < assignedParameters.Length && assignedParameters[nextPositionalIndex])
                {
                    nextPositionalIndex++;
                }

                if (nextPositionalIndex >= assignedParameters.Length)
                {
                    continue;
                }

                parameterIndex = nextPositionalIndex++;
            }

            assignedParameters[parameterIndex] = true;
            if (parameterIndex == index)
            {
                return GeneratorLocationInfo.FromLocation(argument.GetLocation());
            }
        }

        return null;
    }

    public static GeneratorLocationInfo? GetNamedArgumentLocation(this AttributeData? attributeData, string name, CancellationToken cancellationToken = default)
    {
        if (attributeData?.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax syntax || syntax.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (string.Equals(argument.NameEquals?.Name.Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return GeneratorLocationInfo.FromLocation(argument.GetLocation());
            }
        }

        return null;
    }

    public static long? TryGetInt64(this TypedConstant value)
    {
        return TryGetInt64(value.Value);
    }

    public static long? TryGetInt64(this IFieldSymbol field)
    {
        return field.HasConstantValue ? TryGetInt64(field.ConstantValue) : null;
    }

    private static long? TryGetInt64(object? value)
    {
        return value switch
        {
            byte v => v,
            sbyte v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v => unchecked((long)v),
            _ => null
        };
    }
}
