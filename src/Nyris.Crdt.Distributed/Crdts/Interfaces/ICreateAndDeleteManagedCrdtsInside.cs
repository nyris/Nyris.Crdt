using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed
{
    internal interface ICreateAndDeleteManagedCrdtsInside
    {
        ManagedCrdtContext ManagedCrdtContext { set; }

        Task MarkForDeletionLocallyAsync(InstanceId instanceId, CancellationToken cancellationToken = default);
    }
}