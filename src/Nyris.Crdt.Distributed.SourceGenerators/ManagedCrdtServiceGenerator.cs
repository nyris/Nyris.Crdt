using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nyris.Crdt.Distributed.SourceGenerators.Model;
using Scriban;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
    /// <summary>
    /// Some useful resources:
    ///  - https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm
    /// </summary>
    [Generator]
    public class ManagedCrdtServiceGenerator : ISourceGenerator
    {
        // TODO: currently it does not seem possible to reference another project from source generator project, fix when possible
        private const string ManagedCRDTTypeName = "ManagedCRDT";
        private const string PartiallyReplicatedCRDTRegistryTypeName = "PartiallyReplicatedCRDTRegistry";
        private const string OperationBaseClassMetadataName = "Nyris.Crdt.Distributed.Crdts.Operations.Operation`1";
        private const string OperationBaseClass = "Operation";

        private readonly StringBuilder _log = new("/*");

        private static Lazy<IEnumerable<(Template, string)>> Templates { get; } = new(EnumerateTemplates);

        /// <inheritdoc />
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        /// <inheritdoc />
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver) return;

            AnalyzeCandidatesForManagedCrdts(context,
                                             receiver.ManagedCrdtCandidates,
                                             receiver.OperationCandidates,
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

            _log.AppendLine("*/");
            // _log.Clear();
            context.AddSource("Logs", SourceText.From(_log.ToString(), Encoding.UTF8));
        }

        private static IEnumerable<(Template, string)> EnumerateTemplates()
        {
            foreach (var templateFileName in new[]
                     {
                         "ManagedCrdtServiceTemplate.sbntxt",
                         "IManagedCrdtServiceTemplate.sbntxt",
                         "ServiceCollectionExtensionsTemplate.sbntxt"
                     })
            {
                yield return (Template.Parse(EmbeddedResource.GetContent(templateFileName), templateFileName),
                              templateFileName);
            }
        }

        private void AnalyzeCandidatesForManagedCrdts(
            GeneratorExecutionContext context,
            IEnumerable<ClassDeclarationSyntax> candidates,
            IEnumerable<RecordDeclarationSyntax> operationCandidates,
            out List<CrdtInfo> crdtInfos,
            out HashSet<RoutedOperationInfo> operationInfos
        )
        {
            crdtInfos = new List<CrdtInfo>();
            operationInfos = new HashSet<RoutedOperationInfo>();

            // process predefined internal crdts
            var nodeSet = context.Compilation.GetTypeByMetadataName("Nyris.Crdt.Distributed.Crdts.NodeSet");
            if (!TryGetCrdtInfo(nodeSet, out var crdtInfo, out _))
            {
                _log.AppendLine(
                                "Something went wrong - could not get crdtInfo of a known class Nyris.Crdt.Distributed.Crdts.NodeSet");
            }

            crdtInfos.Add(crdtInfo);

            // process user-defined crdts
            foreach (var candidateClass in candidates)
            {
                _log.AppendLine("Analysing class: " + candidateClass.Identifier.ToFullString());
                var namedTypeSymbol = context.Compilation.GetSemanticModel(candidateClass.SyntaxTree)
                                             .GetDeclaredSymbol(candidateClass);
                if (namedTypeSymbol == null)
                {
                    _log.AppendLine("Something went wrong - semantic model did not produce an INamedTypeSymbol");
                    continue;
                }

                if (namedTypeSymbol.IsGenericType)
                {
                    _log.AppendLine("Class is generic - skipping");
                    continue;
                }

                if (!TryGetCrdtInfo(namedTypeSymbol, out crdtInfo, out var operations)) continue;

                foreach (var operation in operations)
                {
                    operationInfos.Add(operation);
                }

                crdtInfos.Add(crdtInfo);
            }
        }

        /// <summary>
        /// Checks symbols inheritance chain and return required info if it is a descendent of ManagedCrdt
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="crdtInfo"></param>
        /// <param name="operations"></param>
        /// <returns>True if symbol is a managedCrdt, false otherwise</returns>
        private bool TryGetCrdtInfo(
            INamedTypeSymbol symbol,
            [NotNullWhen(true)] out CrdtInfo crdtInfo,
            out IEnumerable<RoutedOperationInfo> operations
        )
        {
            var current = symbol.BaseType;
            var operationInfos = (IEnumerable<RoutedOperationInfo>) ArraySegment<RoutedOperationInfo>.Empty;

            while (current != null && current.ToDisplayString() != "object")
            {
                if (current.Name == PartiallyReplicatedCRDTRegistryTypeName)
                {
                    var keyType = current.TypeArguments[0];

                    _log.AppendLine(
                                    $"Class {symbol.Name} determined to be a {PartiallyReplicatedCRDTRegistryTypeName}. " +
                                    "Generated gRPC service will include methods for applying its operations.");

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

                if (current.Name == ManagedCRDTTypeName)
                {
                    _log.AppendLine($"Class {symbol.Name} determined to be a ManagedCRDT. " +
                                    "Generated gRPC service will include transport operations for it's dto");
                    var allArgumentsString = string.Join(", ",
                                                         current.TypeArguments.Select(typeSymbol => typeSymbol.ToDisplayString()));
                    var dtoString = current.TypeArguments.Last().ToDisplayString();

                    crdtInfo = new CrdtInfo(
                                            CrdtTypeName: symbol.ToDisplayString(),
                                            AllArgumentsString: allArgumentsString,
                                            DtoTypeName: dtoString);
                    operations = operationInfos;
                    return true;
                }

                current = current.BaseType;
            }

            crdtInfo = null;
            operations = operationInfos;
            return false;
        }
    }
}
