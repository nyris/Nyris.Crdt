using System.Diagnostics.CodeAnalysis;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Abstractions;

public interface IManagedCrdtProvider
{
    bool TryGet(InstanceId instanceId, [NotNullWhen(true)] out IManagedCrdt? crdt);
}