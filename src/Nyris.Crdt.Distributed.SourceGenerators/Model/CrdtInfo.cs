namespace Nyris.Crdt.Distributed.SourceGenerators.Model
{
    public record CrdtInfo(string CrdtTypeName, string AllArgumentsString, string DtoTypeName)
    {
        public string CrdtTypeName { get; } = CrdtTypeName;
        public string AllArgumentsString { get; } = AllArgumentsString;
        public string DtoTypeName { get; } = DtoTypeName;
    }
}