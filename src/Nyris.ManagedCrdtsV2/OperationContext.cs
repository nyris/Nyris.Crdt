using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public sealed record OperationContext(NodeId Origin, int AwaitPropagationToNNodes = -1, string TraceId = "", CancellationToken CancellationToken = default);