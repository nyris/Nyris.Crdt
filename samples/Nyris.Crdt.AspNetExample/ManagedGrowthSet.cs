using System.Collections.Generic;
using System.Linq;
using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts;

namespace Nyris.Crdt.AspNetExample
{
    internal sealed class GrowthSet : ManagedCRDT<GrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public GrowthSet(int instanceId) : base(instanceId)
        {
        }

        private GrowthSet(HashSet<int> values) : base(-1)
        {
            Value = values;
        }

        /// <inheritdoc />
        public override HashSet<int> Value { get; } = new();

        /// <inheritdoc />
        public override MergeResult Merge(GrowthSet other)
        {
            Value.UnionWith(other.Value);
            return MergeResult.ConflictSolved;
        }

        /// <inheritdoc />
        public override List<int> ToDto() => Value.ToList();

        public static readonly ICRDTFactory<GrowthSet, HashSet<int>, List<int>>
            DefaultFactory = new GrowthSetFactory();

        private sealed class GrowthSetFactory : ICRDTFactory<GrowthSet, HashSet<int>, List<int>>
        {
            /// <inheritdoc />
            public GrowthSet Create(List<int> dto) => new(dto.ToHashSet());
        }
    }
}