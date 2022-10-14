using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Managed.Exceptions;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Services;

internal sealed class NodeInfoProvider : INodeInfoProvider
{
    private NodeInfo? _info;
    private readonly ILogger<NodeInfoProvider> _logger;


    public NodeInfoProvider(ILogger<NodeInfoProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public NodeId ThisNodeId { get; } = Environment.GetEnvironmentVariable("NODE_NAME") is not null
                                            ? new NodeId(Environment.GetEnvironmentVariable("NODE_NAME")!)
                                            : NodeId.GenerateNew();

    public NodeInfo GetMyNodeInfo()
    {
        if (_info != null) return _info;

        var host = Dns.GetHostEntry(Dns.GetHostName());

        foreach (var ip in host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList())
        {
            var uriString = Environment.GetEnvironmentVariable("DEFAULT_URI") ?? $"http://{ip}:{8080}";
            _info = new NodeInfo(new Uri(uriString), ThisNodeId);

            _logger.LogInformation("This node was assigned id={ThisNodeId} and uri={uriString}", 
                ThisNodeId.ToString(), uriString);

            return _info;
        }

        throw new InitializationException("No network adapters with an IPv4 address in the system!");
    }
}
