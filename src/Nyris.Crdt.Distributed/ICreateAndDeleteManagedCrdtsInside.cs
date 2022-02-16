using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed
{
    internal interface ICreateAndDeleteManagedCrdtsInside
    {
        ManagedCrdtContext ManagedCrdtContext { set; }

        Task MarkForDeletionLocallyAsync(string instanceId, CancellationToken cancellationToken = default);
    }
}