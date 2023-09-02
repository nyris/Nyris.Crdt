using System.Collections.Generic;
using System.Collections.Immutable;

namespace Nyris.Crdt.Interfaces
{
    public interface IDeltaCrdt<TDeltaDto, TTimestamp>
    {
        TTimestamp GetLastKnownTimestamp();
        IEnumerable<TDeltaDto> EnumerateDeltaDtos(TTimestamp? timestamp = default);
        DeltaMergeResult Merge(TDeltaDto delta);

        /// <summary>
        /// Attempts to remove all traces of an applied delta. If can't, state should not be updated and false should be returned.
        /// </summary>
        /// <param name="deltaDto"></param>
        /// <returns></returns>
        bool TryReverse(TDeltaDto deltaDto) => false;

        public DeltaMergeResult Merge(ImmutableArray<TDeltaDto> deltas)
        {
            var result = DeltaMergeResult.StateNotChanged;
            foreach (var delta in deltas)
            {
                if (Merge(delta) == DeltaMergeResult.StateUpdated) result = DeltaMergeResult.StateUpdated;
            }

            return result;
        }
    }
}