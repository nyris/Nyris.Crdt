using System.Collections.Generic;

namespace Nyris.Crdt
{
    public interface IDeltaCrdt<TDeltaDto, TTimestamp>
    {
        TTimestamp GetLastKnownTimestamp();
        IEnumerable<TDeltaDto> EnumerateDeltaDtos(TTimestamp? since = default);
        void Merge(TDeltaDto delta);

        public void Merge(IReadOnlyList<TDeltaDto> deltas)
        {
            // ReSharper disable once ForCanBeConvertedToForeach - faster
            for (var i = 0; i < deltas.Count; ++i)
            {
                Merge(deltas[i]);
            }
        }
    }

    public interface IDeltaCrdt<TDeltaDto, TTimestamp, in TOperation> : IDeltaCrdt<TDeltaDto, TTimestamp>
    {
        IReadOnlyList<TDeltaDto> Mutate(TOperation operation);
    }
}