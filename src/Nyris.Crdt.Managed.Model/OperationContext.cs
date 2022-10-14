namespace Nyris.Crdt.Managed.Model;

public sealed record OperationContext(NodeId Origin, int AwaitPropagationToNNodes = -1, string TraceId = "", CancellationToken CancellationToken = default);