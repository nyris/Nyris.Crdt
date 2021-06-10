using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Scriban;

namespace Nyris.Crdt.Distributed.SourceGenerators
{
    [Generator]
    public class DtoPassingServiceGenerator : ISourceGenerator
    {
        /// <inheritdoc />
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        /// <inheritdoc />
        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver) return;

            var builder = new StringBuilder().AppendLine("/*");

            builder.AppendLine("\nUsing directives:\n");
            foreach (var usingDirective in receiver.UsingStatements)
            {
                builder.AppendLine(usingDirective);
            }
            builder.AppendLine("*/");

            context.AddSource("Logs", SourceText.From(builder.ToString(), Encoding.UTF8));

            foreach (var (typeName, location, nArgs) in receiver.ManagedCRDTWithUnknownNumberOfGenericArguments)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    "SourceGenerator",
                    "ManagedCRDT type has unexpected number of generic type arguments",
                    "ManagedCRDT type was expected to have 3 generic arguments, but have {0}. {1} will not be ignored",
                    "Expectations",
                    DiagnosticSeverity.Warning, true), location, nArgs, typeName));
            }

            receiver.UsingStatements.Remove("using Nyris.Crdt.Distributed;"); // generated files are already in that namespace
            var usingStatements = receiver.UsingStatements.OrderBy(u => u).ToList();

            foreach (var templateFileName in new[] {"DtoPassingServiceTemplate.sbntxt", "IDtoPassingServiceTemplate.sbntxt"})
            {
                var template = Template.Parse(EmbeddedResource.GetContent(templateFileName), templateFileName);
                var text = template.Render(new
                {
                    receiver.CrdtTypes,
                    UsingStatements = usingStatements
                }, member => member.Name);
                var source = SourceText.From(text, Encoding.UTF8);
                context.AddSource(templateFileName.Replace("Template.sbntxt", ".generated.cs"), source);
            }
        }
    }
}