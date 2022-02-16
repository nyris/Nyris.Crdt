using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    internal interface IReactToOtherCrdtChange
    {
        Task HandleChangeInAnotherCrdtAsync(string instanceId, CancellationToken cancellationToken = default);
    }
}