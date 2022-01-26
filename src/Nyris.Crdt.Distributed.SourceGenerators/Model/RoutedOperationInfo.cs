namespace Nyris.Crdt.Distributed.SourceGenerators.Model
{
    public sealed record RoutedOperationInfo
    {
        public readonly string OperationType;
        public readonly string OperationResponseType;

        /// <summary>
        /// Routed operation is passed between nodes when PartiallyReplicatedRegistry does not have a collection locally
        /// Key here is a key of that registry, used to identify a collection
        /// </summary>
        public readonly string KeyType;

        public readonly string CrdtTypeParams;

        public RoutedOperationInfo(string operationType, string operationResponseType, string keyType, string crdtTypeParams)
        {
            OperationType = operationType;
            OperationResponseType = operationResponseType;
            KeyType = keyType;
            CrdtTypeParams = crdtTypeParams;
        }
    }
}