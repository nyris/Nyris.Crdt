using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nyris.Crdt.Distributed.SourceGenerators.Model;
using Scriban;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
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
            var classDeclarationsProvider =
                context.SyntaxProvider.CreateSyntaxProvider((node, _) => node is ClassDeclarationSyntax { BaseList: { } },
                                                            (syntaxContext, _) => (ClassDeclarationSyntax) syntaxContext.Node);
            var recordDeclarationsProvider =
                context.SyntaxProvider.CreateSyntaxProvider((node, _) => node is RecordDeclarationSyntax { BaseList: { } },
                                                            (syntaxContext, _) => (RecordDeclarationSyntax) syntaxContext.Node);

            var classAndCompilationProvider =
                context.CompilationProvider.Combine(classDeclarationsProvider.Collect().Combine(recordDeclarationsProvider.Collect()));

            context.RegisterSourceOutput(classAndCompilationProvider, Execute);
        }

        private void Execute(
            SourceProductionContext context,
            (Compilation compilation, (ImmutableArray<ClassDeclarationSyntax> syntaxClasses, ImmutableArray<RecordDeclarationSyntax>
                syntaxRecords) syntaxes)
                syntaxTuple
        )
        {
            var (compilation, syntaxes) = syntaxTuple;
            var (syntaxClasses, syntaxRecords) = syntaxes;

            AnalyzeCandidatesForManagedCrdts(compilation,
                                             syntaxClasses,
                                             syntaxRecords,
                                             out var crdtInfos,
                                             out var operationInfos);

            foreach (var (template, templateFileName) in Templates.Value)
            {
                var text = template.Render(new
                {
                    DtoInfos = crdtInfos
                               .GroupBy(i => i.DtoTypeName)
                               .Select(group => new DtoInfo(group.Key,
                                                            group.Select(i => new TypeWithArguments(i.CrdtTypeName, i.AllArgumentsString))
                                                                 .ToList()))
                               .ToList(),
                    OperationInfos = operationInfos
                }, member => member.Name);

                var source = SourceText.From(text, Encoding.UTF8);

                context.AddSource(templateFileName.Replace("Template.sbntxt", ".generated.cs"), source);
            }
        }

        private static IEnumerable<(Template, string)> EnumerateTemplates() => new[]
        {
            "ManagedCrdtServiceTemplate.sbntxt",
            "IManagedCrdtServiceTemplate.sbntxt",
            "ServiceCollectionExtensionsTemplate.sbntxt"
        }.Select(templateFileName => (Template.Parse(EmbeddedResource.GetContent(templateFileName), templateFileName),
                                      templateFileName));

        private void AnalyzeCandidatesForManagedCrdts(
            Compilation compilation,
            IEnumerable<ClassDeclarationSyntax> candidates,
            // NOTE: Should eventually support Records for CRDT Contaxes
            IEnumerable<RecordDeclarationSyntax> operationCandidates,
            out List<CrdtInfo> crdtInfos,
            out HashSet<RoutedOperationInfo> operationInfos
        )
        {
            crdtInfos = new List<CrdtInfo>();
            operationInfos = new HashSet<RoutedOperationInfo>();

            // process predefined internal crdts
            var nodeSet = compilation.GetTypeByMetadataName("Nyris.Crdt.Distributed.Crdts.NodeSet");
            if (nodeSet is null || !TryGetCrdtInfo(nodeSet, out var crdtInfo, out _))
            {
                throw new MissingMemberException(
                                                 "Something went wrong - could not get crdtInfo of a known class Nyris.Crdt.Distributed.Crdts.NodeSet");
            }

            if (crdtInfo is not null)
            {
                crdtInfos.Add(crdtInfo);
            }

            // process user-defined crdts
            foreach (var candidateClass in candidates)
            {
                // _log.AppendLine("Analyzing class: " + candidateClass.Identifier.ToFullString());
                var namedTypeSymbol = compilation.GetSemanticModel(candidateClass.SyntaxTree)
                                                 .GetDeclaredSymbol(candidateClass);
                if (namedTypeSymbol == null)
                {
                    // _log.AppendLine("Something went wrong - semantic model did not produce an INamedTypeSymbol");
                    continue;
                }

                if (namedTypeSymbol.IsGenericType)
                {
                    // _log.AppendLine("Class is generic - skipping");
                    continue;
                }

                if (!TryGetCrdtInfo(namedTypeSymbol, out crdtInfo, out var operations)) continue;

                foreach (var operation in operations)
                {
                    operationInfos.Add(operation);
                }

                if (crdtInfo is not null)
                {
                    crdtInfos.Add(crdtInfo);
                }
            }
        }

        /// <summary>
        /// Checks symbols inheritance chain and return required info if it is a descendent of ManagedCrdt
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="crdtInfo"></param>
        /// <param name="operations"></param>
        /// <returns>True if symbol is a managedCrdt, false otherwise</returns>
        private static bool TryGetCrdtInfo(
            ITypeSymbol symbol,
            out CrdtInfo? crdtInfo,
            out ImmutableArray<RoutedOperationInfo> operations
        )
        {
            var current = symbol.BaseType;
            var operationInfos = new List<RoutedOperationInfo>();

            while (current != null && current.ToDisplayString() != "object")
            {
                if (current.Name == PartiallyReplicatedCrdtRegistryTypeName)
                {
                    var keyType = current.TypeArguments[0];

                    // _log.AppendLine(
                    //     $"Class {symbol.Name} determined to be a {PartiallyReplicatedCRDTRegistryTypeName}. " +
                    //     "Generated gRPC service will include methods for applying its operations.");

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
                                           }).ToList();
                }

                if (current.Name == ManagedCrdtTypeName)
                {
                    // _log.AppendLine($"Class {symbol.Name} determined to be a ManagedCRDT. " +
                    //                 "Generated gRPC service will include transport operations for it's dto");
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
}
