using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
    public class SyntaxReceiver : ISyntaxReceiver
    {
        public readonly List<ClassDeclarationSyntax> ManagedCrdtCandidates = new();
        public readonly List<RecordDeclarationSyntax> OperationCandidates = new();

        /// <inheritdoc />
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            switch (syntaxNode)
            {
                // ManagedCrdts are classes that ultimately inherit from ManagedCrdt abstract class
                // But that can be by a long inheritance chain, so we consider every class that inherits from someone
                case ClassDeclarationSyntax { BaseList: { } } classDeclaration:
                    ManagedCrdtCandidates.Add(classDeclaration);
                    break;
                // Operations are records that inherit from Operation abstract class. Similarly - take every record that
                // inherit from some other
                case RecordDeclarationSyntax { BaseList: { } } recordDeclaration:
                    OperationCandidates.Add(recordDeclaration);
                    break;
            }
        }
    }
}