using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Services
{
    internal interface IChannelManager
    {
        public bool TryGet<TService>(NodeId nodeId, [NotNullWhen(true)] out TService? grpcClient)
            where TService : class;
    }
}