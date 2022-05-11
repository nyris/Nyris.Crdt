using Nyris.Crdt.Distributed.Model;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces;

internal interface IReactToOtherCrdtChange
{
    Task HandleChangeInAnotherCrdtAsync(InstanceId instanceId, CancellationToken cancellationToken = default);
}
