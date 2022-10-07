using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IMetadataPropagationService
{
    Task PropagateAsync(MetadataDto kind,
        ReadOnlyMemory<byte> data,
        IReadOnlyCollection<NodeInfo> nodesInCluster,
        CancellationToken cancellationToken = default);
}