using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts.Interfaces
{
    internal interface IRebalanceAtNodeChange
    {
        Task RebalanceAsync();
    }
}