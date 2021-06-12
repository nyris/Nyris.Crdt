namespace Nyris.Crdt.Distributed.SourceGenerators
{
    public readonly struct CrdtInfo
    {
        public readonly string CrdtTypeName;
        public readonly string AllArgumentsString;
        public readonly string DtoTypeName;

        public CrdtInfo(string crdtTypeName, string allArgumentsString, string dtoTypeName)
        {
            CrdtTypeName = crdtTypeName;
            AllArgumentsString = allArgumentsString;
            DtoTypeName = dtoTypeName;
        }
    }
}