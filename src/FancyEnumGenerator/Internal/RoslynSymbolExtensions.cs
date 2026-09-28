using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FancyEnumGenerator.Internal;

internal static class RoslynSymbolExtensions
{
    public static bool IsPublicWritableInstanceProperty(this IPropertySymbol property) => !property.IsAbstract && !property.IsStatic && !property.IsReadOnly && !property.IsIndexer && property.DeclaredAccessibility == Accessibility.Public;

    public static bool IsPartial(this INamedTypeSymbol classSymbol)
    {
        foreach (var syntaxRef in classSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is ClassDeclarationSyntax cds &&
                cds.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ImplementsInterface(this ITypeSymbol typeSymbol, string interfaceDisplayName)
    {
        return typeSymbol
            .AllInterfaces
            .Any(i => string.Equals(i.ToDisplayString(), interfaceDisplayName, StringComparison.Ordinal));
    }

    public static bool HasAttribute(this ISymbol symbol, string fullyQualifiedAttributeTypeName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (string.Equals(attribute.AttributeClass?.ToDisplayString(), fullyQualifiedAttributeTypeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryGetAttribute(this ISymbol symbol, string fullyQualifiedAttributeTypeName, [NotNullWhen(true)] out AttributeData? attributeData)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (string.Equals(attribute.AttributeClass?.ToDisplayString(), fullyQualifiedAttributeTypeName, StringComparison.Ordinal))
            {
                attributeData = attribute;
                return true;
            }
        }

        attributeData = null;
        return false;
    }

    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string fullyQualifiedAttributeTypeName)
    {
        return symbol.GetAttributes().Where(attribute => string.Equals(attribute.AttributeClass?.ToDisplayString(), fullyQualifiedAttributeTypeName, StringComparison.Ordinal));
    }
}
