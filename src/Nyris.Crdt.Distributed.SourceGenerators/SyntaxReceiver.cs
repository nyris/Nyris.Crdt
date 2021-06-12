using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
    public class SyntaxReceiver : ISyntaxReceiver
    {
        public readonly List<ClassDeclarationSyntax> Candidates = new();

        /// <inheritdoc />
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // filter out classes that are not implementing anything
            if (syntaxNode is not ClassDeclarationSyntax classDeclarationSyntax
                || classDeclarationSyntax.BaseList is null) return;

            Candidates.Add(classDeclarationSyntax);
        }
    }
}