using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FancyEnumGenerator;

/// <summary>
/// Reports HENUM016 for an enum that uses a <see cref="FancyEnumMemberSetAttribute"/>-marked attribute on a member but
/// has no <see cref="FancyEnumAttribute"/>, so the generator silently produces nothing for it.
/// </summary>
/// <remarks>
/// This lives in an analyzer rather than the generator on purpose: finding such enums means inspecting every
/// attributed enum member in the compilation, which the generator would have to redo on every keystroke. Analyzers
/// run in the background and off the generation path, where that scan costs the editing experience nothing.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberSetWithoutFancyEnumAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [EnumGeneratorDiagnostics.MemberSetWithoutFancyEnum];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static startContext =>
        {
            var fancyEnumAttributeType = startContext.Compilation.GetTypeByMetadataName(typeof(FancyEnumAttribute).FullName!);
            var memberSetAttributeType = startContext.Compilation.GetTypeByMetadataName(typeof(FancyEnumMemberSetAttribute).FullName!);
            if (fancyEnumAttributeType is null || memberSetAttributeType is null)
            {
                return;
            }
            startContext.RegisterSymbolAction(symbolContext => AnalyzeEnum(symbolContext, fancyEnumAttributeType, memberSetAttributeType), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeEnum(SymbolAnalysisContext context, INamedTypeSymbol fancyEnumAttributeType, INamedTypeSymbol memberSetAttributeType)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType ||
            enumType.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, fancyEnumAttributeType)))
        {
            return;
        }
        var memberSet = enumType.GetMembers().OfType<IFieldSymbol>()
            .SelectMany(static field => field.GetAttributes())
            .Select(static attribute => attribute.AttributeClass)
            .FirstOrDefault(attributeClass => attributeClass is not null && attributeClass.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, memberSetAttributeType)));
        if (memberSet is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(EnumGeneratorDiagnostics.MemberSetWithoutFancyEnum, enumType.Locations.FirstOrDefault(), enumType.ToDisplayString(), memberSet.ToDisplayString()));
        }
    }
}
