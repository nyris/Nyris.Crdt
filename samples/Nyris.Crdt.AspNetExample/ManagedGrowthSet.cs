using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ManagedGrowthSet : ManagedCRDT<ManagedGrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public ManagedGrowthSet(string instanceId) : base(instanceId)
        {
        }

        private ManagedGrowthSet(List<int> values) : base("")
        {
            Value = values.ToHashSet();
        }

        /// <inheritdoc />
        public override HashSet<int> Value { get; } = new();

        public void Add(int item)
        {
            Value.Add(item);
            StateChangedAsync();
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(ManagedGrowthSet other)
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
        public override string TypeName { get; } = nameof(ManagedGrowthSet);

        /// <inheritdoc />
        public override async Task<string> GetHashAsync() => Value
            .OrderBy(i => i)
            .Aggregate(0, HashCode.Combine)
            .ToString();

        public static readonly IManagedCRDTFactory<ManagedGrowthSet, HashSet<int>, List<int>>
            DefaultFactory = new GrowthSetFactory();

        public static ManagedGrowthSet FromDto(List<int> dto) => new(dto);
    }

    public sealed class GrowthSetFactory : IManagedCRDTFactory<ManagedGrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public ManagedGrowthSet Create(List<int> dto) => ManagedGrowthSet.FromDto(dto);
    }
}