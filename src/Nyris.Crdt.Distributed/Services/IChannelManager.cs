using Nyris.Crdt.Distributed.Model;
using System.Diagnostics.CodeAnalysis;

namespace Nyris.Crdt.Distributed.Services;

public interface IChannelManager
{
    public bool TryGet<TService>(NodeId nodeId, [NotNullWhen(true)] out TService? grpcClient)
        where TService : class;
}
