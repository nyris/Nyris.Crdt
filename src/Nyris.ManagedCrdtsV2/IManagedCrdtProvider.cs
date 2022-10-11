using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;

public interface IManagedCrdtProvider
{
    ICollection<InstanceId> InstanceIds { get; }

    bool TryGet<TCrdt>(InstanceId instanceId, out TCrdt? crdt) where TCrdt : ManagedCrdt;

    bool TryGet(InstanceId instanceId, [NotNullWhen(true)] out ManagedCrdt? crdt) 
        => TryGet<ManagedCrdt>(instanceId, out crdt);
}