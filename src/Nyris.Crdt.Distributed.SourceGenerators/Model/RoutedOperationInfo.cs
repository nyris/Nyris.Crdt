namespace Nyris.Crdt.Distributed.SourceGenerators.Model
{
    public sealed record RoutedOperationInfo(string? OperationType, string? OperationResponseType, string KeyType, string CrdtTypeParams)
    {
        public readonly string? OperationType = OperationType;
        public readonly string? OperationResponseType = OperationResponseType;

        /// <summary>
        /// Routed operation is passed between nodes when PartiallyReplicatedRegistry does not have a collection locally
        /// Key here is a key of that registry, used to identify a collection
        /// </summary>
        public readonly string KeyType = KeyType;

        public readonly string CrdtTypeParams = CrdtTypeParams;
    }
}
