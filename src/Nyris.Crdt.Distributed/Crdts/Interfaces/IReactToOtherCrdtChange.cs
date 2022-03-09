using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    internal interface IReactToOtherCrdtChange
    {
        Task HandleChangeInAnotherCrdtAsync(InstanceId instanceId, CancellationToken cancellationToken = default);
    }
}