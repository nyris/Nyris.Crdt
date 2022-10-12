using System.Collections.Immutable;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IMetadataPropagationService
{
    Task PropagateAsync(MetadataDto kind,
        ReadOnlyMemory<byte> data,
        ImmutableArray<NodeInfo> nodesInCluster,
        OperationContext context);
}