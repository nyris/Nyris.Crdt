using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Managed.Model;
using Nyris.ManagedCrdtsV2.ManagedCrdts;

namespace Nyris.ManagedCrdtsV2;

public interface ICluster
{
    bool TryGet<TCrdt>(InstanceId instanceId, [NotNullWhen(true)] out TCrdt? crdt)
        where TCrdt : ManagedCrdt;

    Task<TCrdt> CreateAsync<TCrdt>(InstanceId instanceId, CancellationToken cancellationToken)
        where TCrdt : ManagedCrdt;
}