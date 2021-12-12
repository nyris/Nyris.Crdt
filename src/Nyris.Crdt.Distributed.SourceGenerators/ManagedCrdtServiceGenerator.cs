using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Nyris.Crdt.Distributed.SourceGenerators.Model;
using Scriban;

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
        private const string OperationBaseClass = "Nyris.Crdt.Distributed.Crdts.Operation";

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
                            group.Select(i => new TypeWithArguments(i.CrdtTypeName, i.AllArgumentsString)).ToList()))
                        .ToList(),
                    OperationInfos = operationInfos
                }, member => member.Name);
                var source = SourceText.From(text, Encoding.UTF8);
                context.AddSource(templateFileName.Replace("Template.sbntxt", ".generated.cs"), source);
            }

            _log.AppendLine("*/");
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
                yield return (Template.Parse(EmbeddedResource.GetContent(templateFileName), templateFileName), templateFileName);
            }
        }

        private void AnalyzeCandidatesForManagedCrdts(GeneratorExecutionContext context,
            IEnumerable<ClassDeclarationSyntax> candidates,
            IEnumerable<RecordDeclarationSyntax> operationCandidates,
            out List<CrdtInfo> crdtInfos,
            out HashSet<RoutedOperationInfo> operationInfos)
        {
            crdtInfos = new List<CrdtInfo>();
            operationInfos = new HashSet<RoutedOperationInfo>();

            // process predefined internal crdts
            var nodeSet = context.Compilation.GetTypeByMetadataName("Nyris.Crdt.Distributed.Crdts.NodeSet");
            if (!TryGetCrdtInfo(nodeSet, out var crdtInfo, out _))
            {
                _log.AppendLine("Something went wrong - could not get crdtInfo of a known class Nyris.Crdt.Distributed.Crdts.NodeSet");
            }
            crdtInfos.Add(crdtInfo);

            // process user-defined crdts
            var operationInfosCandidates = new List<KeyOperationPairCandidate>();
            foreach (var candidateClass in candidates)
            {
                _log.AppendLine("Analysing class: " + candidateClass.Identifier.ToFullString());
                var namedTypeSymbol = context.Compilation.GetSemanticModel(candidateClass.SyntaxTree).GetDeclaredSymbol(candidateClass);
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

                if (!TryGetCrdtInfo(namedTypeSymbol, out crdtInfo, out var operationCandidate)) continue;

                crdtInfos.Add(crdtInfo);
                if (operationCandidate != null) operationInfosCandidates.Add(operationCandidate);
            }

            // construct operation records tree
            var operationsTree = new TypeSymbolNode(context.Compilation.GetTypeByMetadataName(OperationBaseClass));
            var typeToNode = new Dictionary<ITypeSymbol, TypeSymbolNode>(SymbolEqualityComparer.Default);

            foreach (var recordDeclaration in operationCandidates)
            {
                _log.AppendLine("Analysing record: " + recordDeclaration.Identifier.ToFullString());
                var namedTypeSymbol = context.Compilation.GetSemanticModel(recordDeclaration.SyntaxTree).GetDeclaredSymbol(recordDeclaration);
                if (namedTypeSymbol == null)
                {
                    _log.AppendLine("Something went wrong - semantic model did not produce an INamedTypeSymbol");
                    continue;
                }
                PopulateOperationRecordsTree(namedTypeSymbol, operationsTree, typeToNode);
            }

            // foreach operation-key candidate, traverse descendents of operation to get all concrete pairs
            _log.AppendLine("Examining candidates to get all descendents of operations");
            foreach (var op in operationInfosCandidates)
            {
                if (!typeToNode.ContainsKey(op.OperationType))
                {
                    _log.AppendLine($"Something went wrong - {op.OperationType.ToDisplayString()} was not found in a tree");
                    continue;
                }

                foreach (var symbolNode in typeToNode[op.OperationType]
                             .Traverse()
                             .Where(node => !node.Value.IsAbstract))
                {
                    operationInfos.Add(new RoutedOperationInfo(symbolNode.Value.ToDisplayString(), op.Key, op.CrdtTypeParams));
                }
            }
        }

        /// <summary>
        /// Traverse a given symbol ancestors. If symbol has an ancestor that is at a given root,
        /// adds entire chain to the tree. Thus - when given a particular class or record at the root
        /// and called on a list of other classes/records that inherit from it, it reconstructs an accurate
        /// inheritance tree (i.e. mapping from a class/record to all it's children)
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="root"></param>
        /// <param name="typeToNode"></param>
        private static void PopulateOperationRecordsTree(INamedTypeSymbol symbol,
            TypeSymbolNode root,
            IDictionary<ITypeSymbol, TypeSymbolNode> typeToNode)
        {
            var classHierarchy = new Stack<ITypeSymbol>();

            for (var current = symbol;
                 current != null && current.ToDisplayString() != "object";
                 current = current.BaseType)
            {
                // if we encountered record that is at the root, merge entire chain with the tree, in reverse order
                if (SymbolEqualityComparer.Default.Equals(current, root.Value))
                {
                    root.Add(classHierarchy, typeToNode);
                }
                // else continue to grow the chain
                else classHierarchy.Push(current);
            }
        }

        /// <summary>
        /// Checks symbols inheritance chain and return required info if it is a descendent of ManagedCrdt
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="crdtInfo"></param>
        /// <param name="keyOperationPairCandidate"></param>
        /// <returns>True if symbol is a managedCrdt, false otherwise</returns>
        private bool TryGetCrdtInfo(INamedTypeSymbol symbol,
            [NotNullWhen(true)] out CrdtInfo crdtInfo,
            [MaybeNullWhen(true)] out KeyOperationPairCandidate keyOperationPairCandidate)
        {
            var current = symbol.BaseType;
            KeyOperationPairCandidate operationCandidate = null;

            while (current != null && current.ToDisplayString() != "object")
            {
                if (current.Name == PartiallyReplicatedCRDTRegistryTypeName)
                {
                    var keyType = current.TypeArguments.First();
                    var operationType = current.TypeArguments.Last();

                    var collectionType = current.TypeArguments[1].ToDisplayString();
                    _log.AppendLine($"Class {symbol.Name} determined to be a {PartiallyReplicatedCRDTRegistryTypeName}. " +
                                    "Generated gRPC service will include transport operations for operations " +
                                    $"of {collectionType} (descendent records of {operationType.ToDisplayString()})");

                    var otherParams = current.TypeArguments.Skip(3).Take(4).Select(s => s.ToDisplayString());
                    var crdtTypeParams = $"{collectionType}, {string.Join(", ", otherParams)}";

                    operationCandidate = new KeyOperationPairCandidate(keyType.ToDisplayString(), operationType, crdtTypeParams);
                    _log.Append("Adding candidate for operation-key pair: ")
                        .Append(operationCandidate.OperationType.ToDisplayString())
                        .Append(" - ")
                        .AppendLine(operationCandidate.Key);
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
                    keyOperationPairCandidate = operationCandidate;
                    return true;
                }

                current = current.BaseType;
            }

            crdtInfo = null;
            keyOperationPairCandidate = null;
            return false;
        }

        private record KeyOperationPairCandidate(string Key, ITypeSymbol OperationType, string CrdtTypeParams)
        {
            public string Key { get; } = Key;
            public ITypeSymbol OperationType { get; } = OperationType;
            public string CrdtTypeParams { get; } = CrdtTypeParams;
        }

        private class TypeSymbolNode
        {
            public readonly ITypeSymbol Value;
            public readonly Dictionary<ITypeSymbol, TypeSymbolNode> Nodes;

            public TypeSymbolNode(ITypeSymbol value)
            {
                Value = value;
                Nodes = new Dictionary<ITypeSymbol, TypeSymbolNode>(SymbolEqualityComparer.Default);
            }

            /// <summary>
            /// Add a list of symbols to a tree. i-th symbol in a list is assumed to be inherited from (i-1)-th symbol
            /// </summary>
            /// <param name="symbols"></param>
            /// <param name="typeToNode">A mapping from symbol to nodes, to which newly created nodes will be added.</param>
            public void Add(IEnumerable<ITypeSymbol> symbols, IDictionary<ITypeSymbol, TypeSymbolNode> typeToNode)
            {
                var current = this;
                foreach (var symbol in symbols)
                {
                    if (!current.Nodes.ContainsKey(symbol))
                    {
                        var newNode = new TypeSymbolNode(symbol);
                        current.Nodes[symbol] = newNode;
                        typeToNode[symbol] = newNode;
                    }
                    current = current.Nodes[symbol];
                }
            }

            public IEnumerable<TypeSymbolNode> Traverse()
            {
                yield return this;
                if (Nodes.Count <= 0) yield break;

                foreach (var node in Nodes.Values.SelectMany(node => node.Traverse()))
                {
                    yield return node;
                }
            }
        }
    }
}
