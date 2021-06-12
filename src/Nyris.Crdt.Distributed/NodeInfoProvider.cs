using System;
using System.Net;
using System.Net.Sockets;
using Nyris.Crdt.Distributed.Model;
using Nyris.Experiments.Consensus.Server.Exceptions;

namespace Nyris.Crdt.Distributed
{
    internal static class NodeInfoProvider
    {
        public static NodeInfo GetMyNodeInfo()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var nodeId = NodeId.FromGuid(Guid.NewGuid());
                    Console.WriteLine($"This node was assigned id={nodeId}");
                    return new NodeInfo(new Uri($"http://{ip}"), nodeId);
                }
            }

            throw new InitializationException("No network adapters with an IPv4 address in the system!");
        }
    }
}