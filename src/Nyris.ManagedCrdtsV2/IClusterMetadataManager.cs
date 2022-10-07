using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IClusterMetadataManager
{
    Task<ReadOnlyMemory<byte>> AddNewNodeAsync(NodeInfo nodeInfo, CancellationToken cancellationToken = default);
    Task MergeAsync(MetadataDto kind, ReadOnlyMemory<byte> dto, CancellationToken cancellationToken = default);
}