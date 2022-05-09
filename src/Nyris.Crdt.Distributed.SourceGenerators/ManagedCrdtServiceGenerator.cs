using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nyris.Crdt.Distributed.SourceGenerators.Model;
using Scriban;


namespace Nyris.Crdt.Distributed.SourceGenerators;

/// <summary>
/// Some useful resources:
///  - https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm
/// </summary>
[Generator]
public class ManagedCrdtServiceGenerator : IIncrementalGenerator
{
    // TODO: currently it does not seem possible to reference another project from source generator project, fix when possible
    private const string ManagedCrdtTypeName = "ManagedCRDT";
    private const string PartiallyReplicatedCrdtRegistryTypeName = "PartiallyReplicatedCRDTRegistry";

    private static Lazy<IEnumerable<(Template, string)>> Templates { get; } = new(EnumerateTemplates);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classCandidatesProvider =
            context.SyntaxProvider.CreateSyntaxProvider(
                (syntaxNode, _) =>
                    syntaxNode is ClassDeclarationSyntax { BaseList: { } },
                (syntaxContext, _) => (ClassDeclarationSyntax) syntaxContext.Node);
        var recordCandidatesProvider =
            context.SyntaxProvider.CreateSyntaxProvider(
                (syntaxNode, _) => syntaxNode is RecordDeclarationSyntax { BaseList: { } },
                (syntaxContext, _) => (RecordDeclarationSyntax) syntaxContext.Node);


        var candidates = classCandidatesProvider.Combine(recordCandidatesProvider.Collect());

        var candidatesWithCompilationProvider = context.CompilationProvider.Combine(candidates.Collect());

        context.RegisterSourceOutput(candidatesWithCompilationProvider, Execute);
    }

    private void Execute(SourceProductionContext context,
        (Compilation compilation, ImmutableArray<(ClassDeclarationSyntax, ImmutableArray<RecordDeclarationSyntax>)> syntaxs) tuple)
    {
        var candidates = AnalyzeCandidatesForManagedCrdts(tuple);
        var (crdtInfos, operationInfos) = candidates;

        foreach (var (template, templateFileName) in Templates.Value)
        {
            var source = template.Render(new
            {
                DtoInfos = crdtInfos
                    .GroupBy(i => i.DtoTypeName)
                    .Select(group => new DtoInfo(group.Key,
                        group.Select(i => new TypeWithArguments(i.CrdtTypeName, i.AllArgumentsString)).ToList()))
                    .ToList(),
                OperationInfos = operationInfos
            }, member => member.Name);

            context.AddSource(templateFileName.Replace("Template.sbntxt", ".g.cs"),
                SourceText.From(source, Encoding.UTF8));
        }
    }

    private static IEnumerable<(Template, string)> EnumerateTemplates()
    {
        return new[]
        {
            "ManagedCrdtServiceTemplate.sbntxt",
            "IManagedCrdtServiceTemplate.sbntxt",
            "ServiceCollectionExtensionsTemplate.sbntxt"
        }.Select(templateFileName => (Template.Parse(EmbeddedResource.GetContent(templateFileName), templateFileName),
            templateFileName));
    }

    private (HashSet<CrdtInfo>, ImmutableArray<RoutedOperationInfo>) AnalyzeCandidatesForManagedCrdts(
        (Compilation compilation, ImmutableArray<(ClassDeclarationSyntax, ImmutableArray<RecordDeclarationSyntax>)> syntaxs) tuple)
    {
        var compilation = tuple.compilation;
        var crdtInfos = new HashSet<CrdtInfo>();
        var operationInfos = new ImmutableArray<RoutedOperationInfo>();
        var foundTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var (classes, records) in tuple.syntaxs)
        {
            var candidateClasses = records.CastArray<TypeDeclarationSyntax>().Add(classes);

            // process predefined internal crdts
            var nodeSet = compilation.GetTypeByMetadataName("Nyris.Crdt.Distributed.Crdts.NodeSet");

            foundTypes.Add(nodeSet);

            if (TryGetCrdtInfo(nodeSet, out var internalCrdtInfo, out operationInfos) && internalCrdtInfo is not null)
            {
                crdtInfos.Add(internalCrdtInfo);
            }

            foreach (var candidateClass in candidateClasses)
            {
                var classSemanticModel = compilation.GetSemanticModel(candidateClass.SyntaxTree);
                var symbol = classSemanticModel.GetDeclaredSymbol(candidateClass);

                if (symbol is null || foundTypes.Contains(symbol))
                {
                    // NOTE: Ignore Type already processed
                    continue;
                }

                foundTypes.Add(symbol);

                // process user-defined crdts
                var namedTypeSymbol = compilation.GetSemanticModel(candidateClass.SyntaxTree)
                    .GetDeclaredSymbol(candidateClass);
                if (namedTypeSymbol == null)
                {
                    return (crdtInfos, operationInfos);
                }

                if (namedTypeSymbol.IsGenericType)
                {
                    return (crdtInfos, operationInfos);
                }

                if (!TryGetCrdtInfo(namedTypeSymbol, out var crdtInfo, out var operations)) return (crdtInfos, operationInfos);

                if (crdtInfo is not null && !crdtInfos.Contains(crdtInfo))
                {
                    crdtInfos.Add(crdtInfo);
                }

                operationInfos = Enumerable.Aggregate(operations, operationInfos, (current, operation) => current.Add(operation));
            }
        }

        return (crdtInfos, operationInfos);
    }

    /// <summary>
    /// Checks symbols inheritance chain and return required info if it is a descendent of ManagedCrdt
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="crdtInfo"></param>
    /// <param name="operations"></param>
    /// <returns>True if symbol is a managedCrdt, false otherwise</returns>
    private static bool TryGetCrdtInfo(ITypeSymbol symbol,
        out CrdtInfo crdtInfo,
        out ImmutableArray<RoutedOperationInfo> operations)
    {
        var current = symbol.BaseType;
        var operationInfos = Enumerable.Empty<RoutedOperationInfo>();

        while (current != null && current.ToDisplayString() != "object")
        {
            if (current.Name == PartiallyReplicatedCrdtRegistryTypeName)
            {
                var keyType = current.TypeArguments[0];

                var crdtTypeParams = string.Join(", ", current.TypeArguments.Select(s => s.ToDisplayString()));

                // get attributes of symbol
                // get type arguments of constructor

                operationInfos = symbol.GetAttributes()
                    .Where(ad => ad.AttributeClass?.Name == "RequireOperationAttribute")
                    .Select(attr =>
                    {
                        var operationConcreteType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
                        var operationResponseConcreteType = attr.ConstructorArguments[1].Value as INamedTypeSymbol;

                        return new RoutedOperationInfo(operationConcreteType?.ToDisplayString(),
                            operationResponseConcreteType?.ToDisplayString(),
                            keyType.ToDisplayString(),
                            $"{symbol.ToDisplayString()}, {crdtTypeParams}");
                    });
            }

            if (current.Name == ManagedCrdtTypeName)
            {
                var allArgumentsString = string.Join(", ",
                    current.TypeArguments.Select(typeSymbol => typeSymbol.ToDisplayString()));
                var dtoString = current.TypeArguments.Last().ToDisplayString();

                crdtInfo = new CrdtInfo(
                    CrdtTypeName: symbol.ToDisplayString(),
                    AllArgumentsString: allArgumentsString,
                    DtoTypeName: dtoString);
                operations = operationInfos.ToImmutableArray();
                return true;
            }

            current = current.BaseType;
        }

        crdtInfo = null;
        operations = operationInfos.ToImmutableArray();
        return false;
    }
}
