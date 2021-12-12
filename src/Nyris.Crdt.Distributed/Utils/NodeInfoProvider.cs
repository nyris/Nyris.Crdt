using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils
{
    internal static class NodeInfoProvider
    {
        public static readonly NodeId ThisNodeId = NodeId.FromGuid(Guid.NewGuid());
        private static NodeInfo? _info;

        public static NodeInfo GetMyNodeInfo()
        {
            if (_info != null) return _info;

            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine($"This node was assigned id={ThisNodeId}");
                    _info = new NodeInfo(new Uri($"http://{ip}:{8080}"), ThisNodeId);
                    return _info;
                }
            }

            JsonConvert.SerializeObject(_info);

            throw new InitializationException("No network adapters with an IPv4 address in the system!");
        }
    }
}