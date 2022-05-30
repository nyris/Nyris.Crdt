using System.Reflection;

namespace Nyris.Crdt.Distributed.Metrics;

public class MetricsOptions
{
    public string MetricsPrefix { get; set; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "crdt_app";
}
