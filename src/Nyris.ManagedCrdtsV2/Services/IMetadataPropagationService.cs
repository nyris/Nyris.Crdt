using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.ManagedCrdtsV2.Services;

public interface IMetadataPropagationService
{
    Task PropagateAsync(MetadataDto kind,
        ReadOnlyMemory<byte> data,
        ImmutableArray<NodeInfo> nodesInCluster,
        OperationContext context);
}