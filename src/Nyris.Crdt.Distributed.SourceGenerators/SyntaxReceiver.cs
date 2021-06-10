using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
    public readonly struct CrdtInfo
    {
        public readonly string CrdtTypeName;
        public readonly string CrdtRepresentationTypeName;
        public readonly string DtoTypeName;

        public CrdtInfo(string crdtTypeName, string crdtRepresentationTypeName, string dtoTypeName)
        {
            CrdtTypeName = crdtTypeName;
            CrdtRepresentationTypeName = crdtRepresentationTypeName;
            DtoTypeName = dtoTypeName;
        }
    }

    public class SyntaxReceiver : ISyntaxReceiver
    {
        // TODO: figure out optimal way to pass it as nameof()
        private const string ManagedCRDTTypeName = "ManagedCRDT";

        public readonly List<CrdtInfo> CrdtTypes = new ();
        public readonly HashSet<string> UsingStatements = new ();

        public readonly List<(string, Location, int)> ManagedCRDTWithUnknownNumberOfGenericArguments = new();

        /// <inheritdoc />
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // filter out classes that are not implementing anything
            if (syntaxNode is not ClassDeclarationSyntax classDeclarationSyntax
                || classDeclarationSyntax.BaseList is null) return;

            // We are interested in classes that inherit ManagedCRDT.
            // Since it is a class, it must be the first in the list of base type
            // and if it's not, then checking the rest of base types is useless (no multiple inheritance in c#)
            var maybeBaseClass = classDeclarationSyntax.BaseList.Types.First();
            if (maybeBaseClass.DescendantTokens().First().Text != ManagedCRDTTypeName) return;

            var descendants = maybeBaseClass.DescendantNodes().ToArray();
            // first descendent is the base class itself, second - all it's generic arguments,
            // the rest - arguments of those arguments, if any.
            //
            // For example:
            // "ISomeInterface<SomeType<string, int>, OtherType>" will produce the following list of descendents:
            // 1. ISomeInterface<SomeType<string, int>, OtherType>
            // 2. <SomeType<string, int>, OtherType>
            // 3. SomeType<string, int>
            // 4. <string, int>
            // 5. string
            // 6. int
            // 7. OtherType
            //
            // So, we are skipping first two nodes, and then taking only those, that have second node as parent.
            // From example, that would be nodes:
            // 3. SomeType<string, int>
            // 7. OtherType
            var typeArguments = descendants.Skip(2).Where(node => node.Parent == descendants[1]).ToArray();

            if (typeArguments.Length != 3)
            {
                ManagedCRDTWithUnknownNumberOfGenericArguments.Add(
                    (syntaxNode.GetText().ToString(), syntaxNode.GetLocation(), typeArguments.Length));
                return;
            }

            CrdtTypes.Add(new CrdtInfo(typeArguments[0].ToFullString(),
                typeArguments[1].ToFullString(),
                typeArguments[2].ToFullString()));

            // In order for generated services to work, we need to add required using statements.
            // The only solution I could think of, is to add ALL usings from files where CRDT types are defined.
            // This is likely to create unnecessary references, but at least we have a guarantee that
            // generated file references everything it needs
            // TODO: is there a better wat to get a list of required references based on generated code?
            var namespaceDeclaration = classDeclarationSyntax.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
            if (namespaceDeclaration == null) return;

            UsingStatements.Add("using " + namespaceDeclaration.Name + ";");
            UsingStatements.UnionWith(namespaceDeclaration.Usings.Select(u => u.GetText().ToString().TrimEnd('\n')));

            var compilationUnitSyntax = namespaceDeclaration.FirstAncestorOrSelf<CompilationUnitSyntax>();
            if (compilationUnitSyntax == null) return;

            UsingStatements.UnionWith(compilationUnitSyntax.Usings.Select(u => u.GetText().ToString().TrimEnd('\n')));
        }
    }
}