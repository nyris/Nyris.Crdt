using System;
using System.Net;
using System.Net.Sockets;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Services
{
    internal static class NodeInfoProvider
    {
        private static readonly NodeId NodeId = NodeId.FromGuid(Guid.NewGuid());
        private static NodeInfo? _info;

        public static NodeInfo GetMyNodeInfo()
        {
            if (_info != null) return _info;

            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine($"This node was assigned id={NodeId}");
                    _info = new NodeInfo(new Uri($"http://{ip}:{8080}"), NodeId);
                    return _info;
                }
            }

            throw new InitializationException("No network adapters with an IPv4 address in the system!");
        }
    }
}