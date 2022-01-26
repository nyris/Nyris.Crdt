using System;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Exceptions;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Utils
{
    internal sealed class NodeInfoProvider : INodeInfoProvider
    {
        private NodeInfo? _info;

        /// <inheritdoc />
        public NodeId ThisNodeId { get; } = NodeId.FromGuid(Guid.NewGuid());

        public NodeInfo GetMyNodeInfo()
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