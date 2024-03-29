using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Nyris.Crdt.Distributed.Utils;

internal sealed class NodeInfoProvider : INodeInfoProvider
{
    private NodeInfo? _info;

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

            Console.WriteLine($"This node was assigned id={ThisNodeId} and uri={uriString}");

            return _info;
        }

        throw new InitializationException("No network adapters with an IPv4 address in the system!");
    }
}
