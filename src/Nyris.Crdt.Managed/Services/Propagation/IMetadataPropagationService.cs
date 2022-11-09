using System.Collections.Immutable;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Services.Propagation;

public interface IMetadataPropagationService
{
    Task PropagateAsync(MetadataKind kind,
        ReadOnlyMemory<byte> data,
        ImmutableArray<NodeInfo> nodesInCluster,
        OperationContext context);
}