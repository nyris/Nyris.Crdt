using System.Collections.Generic;

namespace Nyris.Crdt
{
    public interface IDeltaCRDT<TDeltaDto, TTimestamp>
    {
        TTimestamp GetLastKnownTimestamp();
        IEnumerable<TDeltaDto> EnumerateDeltaDtos(TTimestamp? since = default);
        void Merge(TDeltaDto delta);
    }
}