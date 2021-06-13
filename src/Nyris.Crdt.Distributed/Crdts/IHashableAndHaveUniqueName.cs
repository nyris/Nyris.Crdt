using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Crdts
{
    internal interface IHashableAndHaveUniqueName
    {
        string TypeName { get; }

        Task<string> GetHashAsync();
    }
}