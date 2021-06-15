using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Scriban;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
    /// <summary>
    /// Some useful resources:
    ///  - https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm
    /// </summary>
    [Generator]
    public class DtoPassingServiceGenerator : ISourceGenerator
    {
        // TODO: currently it does not seem possible to reference another project from source generator project, fix when possible
        private const string ManagedCRDTTypeName = "ManagedCRDT";

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

            var crdtInfos = GetCrdtInfos(context, receiver.Candidates);

            foreach (var (template, templateFileName) in Templates.Value)
            {
                var text = template.Render(new
                {
                    CrdtInfos = crdtInfos
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
                "DtoPassingServiceTemplate.sbntxt",
                "IDtoPassingServiceTemplate.sbntxt",
                "ServiceCollectionExtensionsTemplate.sbntxt"
            })
            {
                yield return (Template.Parse(EmbeddedResource.GetContent(templateFileName), templateFileName), templateFileName);
            }
        }

        private List<CrdtInfo> GetCrdtInfos(GeneratorExecutionContext context, IEnumerable<ClassDeclarationSyntax> candidates)
        {
            var crdtInfos = new List<CrdtInfo>();

            // process predefined internal crdts
            var nodeSet = context.Compilation.GetTypeByMetadataName("Nyris.Crdt.Distributed.Crdts.NodeSet");
            TraverseNamedTypeSymbolInheritanceChain(nodeSet, crdtInfos);

            // process user-defined crdts
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

                TraverseNamedTypeSymbolInheritanceChain(namedTypeSymbol, crdtInfos);
            }

            return crdtInfos;
        }

        private void TraverseNamedTypeSymbolInheritanceChain(INamedTypeSymbol symbol, List<CrdtInfo> crdtInfos)
        {
            var current = symbol.BaseType;
            while (current != null && current.ToDisplayString() != "object")
            {
                if (current.Name != ManagedCRDTTypeName)
                {
                    current = current.BaseType;
                    continue;
                }

                _log.AppendLine($"Class {symbol.Name} determined to be a ManagedCRDT. " +
                                "Generated gRPC service will include transport operations for it's dto");
                var allArgumentsString = string.Join(", ",
                    current.TypeArguments.Select(typeSymbol => typeSymbol.ToDisplayString()));
                var dtoString = current.TypeArguments.Last().ToDisplayString();

                crdtInfos.Add(new CrdtInfo(
                    crdtTypeName: symbol.ToDisplayString(),
                    allArgumentsString: allArgumentsString,
                    dtoTypeName: dtoString));
                return;
            }
        }
    }
}
