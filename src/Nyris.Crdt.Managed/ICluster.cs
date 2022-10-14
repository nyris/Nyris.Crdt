using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Managed.ManagedCrdts;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed;

public interface ICluster
{
    bool TryGet<TCrdt>(InstanceId instanceId, [NotNullWhen(true)] out TCrdt? crdt)
        where TCrdt : ManagedCrdt;

    Task<TCrdt> CreateAsync<TCrdt>(InstanceId instanceId, CancellationToken cancellationToken)
        where TCrdt : ManagedCrdt;
}