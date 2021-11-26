using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ManagedGrowthSet : ManagedCRDT<ManagedGrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public ManagedGrowthSet(string instanceId) : base(instanceId)
        {
        }

        private ManagedGrowthSet(WithId<List<int>> values) : base(values.Id)
        {
            Value = values.Dto?.ToHashSet() ?? new HashSet<int>();
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
        public override Task<List<int>> ToDtoAsync() => Task.FromResult(Value.ToList());

        /// <inheritdoc />
        public override async IAsyncEnumerable<List<int>> EnumerateDtoBatchesAsync()
        {
            foreach (var i in Value)
            {
                yield return new List<int> {i};
            }
        }

        /// <inheritdoc />
        public override string TypeName { get; } = nameof(ManagedGrowthSet);

        /// <inheritdoc />
        public override ReadOnlySpan<byte> GetHash()
        {
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            foreach(var v in Value.OrderBy(i => i))
            {
                md5.AppendData(BitConverter.GetBytes(v));
            }
            return md5.GetCurrentHash();
        }

        public static readonly IManagedCRDTFactory<ManagedGrowthSet, HashSet<int>, List<int>>
            DefaultFactory = new GrowthSetFactory();

        public static ManagedGrowthSet FromDto(WithId<List<int>> dto) => new(dto);
    }

    public sealed class GrowthSetFactory : IManagedCRDTFactory<ManagedGrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public ManagedGrowthSet Create(WithId<List<int>> dto) => ManagedGrowthSet.FromDto(dto);
    }
}
