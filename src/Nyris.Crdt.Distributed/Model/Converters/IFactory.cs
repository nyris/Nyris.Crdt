using System;

namespace Nyris.Crdt.Distributed.Model.Converters
{
    public interface IFactory<out TInternalId>
        where TInternalId : IFormattable
    {
        TInternalId Empty { get; }

        TInternalId Parse(string value);
    }
}
