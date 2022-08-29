using System.Collections.Generic;
using System.Collections.Immutable;

namespace Nyris.Crdt.Interfaces
{
    public interface IDeltaCrdt<TDeltaDto, TTimestamp>
    {
        TTimestamp GetLastKnownTimestamp();
        IEnumerable<TDeltaDto> EnumerateDeltaDtos(TTimestamp? timestamp = default);
        void Merge(TDeltaDto delta);

        public void Merge(ImmutableArray<TDeltaDto> deltas)
        {
            foreach (var delta in deltas)
            {
                Merge(delta);
            }
        }
    }

    // public interface IDeltaCrdt<TDeltaDto, TTimestamp, in TOperation> : IDeltaCrdt<TDeltaDto, TTimestamp>
    // {
    //     IReadOnlyList<TDeltaDto> Mutate(TOperation operation);
    // }
}