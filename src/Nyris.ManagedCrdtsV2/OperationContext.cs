namespace Nyris.ManagedCrdtsV2;

public sealed class OperationContext
{
    public static OperationContext Default { get; } = new();
    
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    public uint PropagateToNNodes { get; set; } = 0;
    public string TraceId { get; set; } = "";
}