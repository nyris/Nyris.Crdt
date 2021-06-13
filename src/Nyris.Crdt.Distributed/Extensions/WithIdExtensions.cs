
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Extensions
{
    internal static class WithIdExtensions
    {
        public static WithId<T> WithId<T>(this T value, string id) => new() {Dto = value, Id = id};
    }
}
