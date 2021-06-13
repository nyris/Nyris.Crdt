using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public void Add(int item)
        {
            Value.Add(item);
            StateChangedAsync();
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(GrowthSet other)
        {
            Value.UnionWith(other.Value);
            await StateChangedAsync();
            return MergeResult.ConflictSolved;
        }

        /// <inheritdoc />
        public override async Task<List<int>> ToDtoAsync() => Value.ToList();

        /// <inheritdoc />
        public override IAsyncEnumerable<List<int>> EnumerateDtoBatchesAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override string TypeName { get; } = nameof(GrowthSet);

        /// <inheritdoc />
        public override async Task<string> GetHashAsync() => Value
            .OrderBy(i => i)
            .Aggregate(0, HashCode.Combine)
            .ToString();

        public static readonly IAsyncCRDTFactory<GrowthSet, HashSet<int>, List<int>>
            DefaultFactory = new GrowthSetFactory();

        private sealed class GrowthSetFactory : IAsyncCRDTFactory<GrowthSet, HashSet<int>, List<int>>
        {
            /// <inheritdoc />
            public GrowthSet Create(List<int> dto) => new(dto.ToHashSet());
        }
    }
}