namespace Nyris.Crdt.Distributed.SourceGenerators.Model
{
    public record TypeWithArguments(string TypeName, string AllArgumentsString)
    {
        public string TypeName { get; } = TypeName;
        public string AllArgumentsString { get; } = AllArgumentsString;
        public string SafeTypeName { get; } = TypeName.Replace('.', '_');
    }
}