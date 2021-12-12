using System.Collections.Generic;

namespace Nyris.Crdt.Distributed.SourceGenerators.Model
{
    public record DtoInfo(string DtoTypeName, List<TypeWithArguments> CrdtInfos)
    {
        public string DtoTypeName { get; } = DtoTypeName;
        public List<TypeWithArguments> CrdtInfos { get; } = CrdtInfos;
    }
}