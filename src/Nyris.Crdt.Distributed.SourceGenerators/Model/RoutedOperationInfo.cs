namespace Nyris.Crdt.Distributed.SourceGenerators.Model
{
    public readonly struct RoutedOperationInfo
    {
        public readonly string OperationType;

        /// <summary>
        /// Routed operation is passed between nodes when PartiallyReplicatedRegistry does not have a collection locally
        /// Key here is a key of that registry, used to identify a collection
        /// </summary>
        public readonly string KeyType;

        public readonly string CrdtTypeParams;

        public RoutedOperationInfo(string operationType, string keyType, string crdtTypeParams)
        {
            OperationType = operationType;
            KeyType = keyType;
            CrdtTypeParams = crdtTypeParams;
        }
    }
}