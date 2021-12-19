using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Abstractions;
using Nyris.Crdt.Distributed.Crdts.Interfaces;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ManagedGrowthSet : ManagedCRDT<ManagedGrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public ManagedGrowthSet(string instanceId) : base(instanceId)
        {
        }

        private ManagedGrowthSet(List<int> values, string instanceId) : base(instanceId)
        {
            Value = values?.ToHashSet() ?? new HashSet<int>();
        }

        /// <inheritdoc />
        public override HashSet<int> Value { get; } = new();

        public void Add(int item)
        {
            Value.Add(item);
            StateChangedAsync();
        }

        /// <inheritdoc />
        public override async Task<MergeResult> MergeAsync(ManagedGrowthSet other,
            CancellationToken cancellationToken = default)
        {
            Value.UnionWith(other.Value);
            await StateChangedAsync();
            return MergeResult.ConflictSolved;
        }

        /// <param name="cancellationToken"></param>
        /// <inheritdoc />
        public override Task<List<int>> ToDtoAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value.ToList());

        /// <inheritdoc />
        public override async IAsyncEnumerable<List<int>> EnumerateDtoBatchesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var i in Value)
            {
                yield return new List<int> {i};
            }
        }

        // /// <inheritdoc />
        // public override string TypeName { get; } = nameof(ManagedGrowthSet);

        /// <inheritdoc />
        public override ReadOnlySpan<byte> CalculateHash()
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

        public static ManagedGrowthSet FromDto(List<int> dto, string instanceId) => new(dto, instanceId);
    }

    public sealed class GrowthSetFactory : IManagedCRDTFactory<ManagedGrowthSet, HashSet<int>, List<int>>
    {
        /// <inheritdoc />
        public ManagedGrowthSet Create(List<int> dto, string instanceId) => ManagedGrowthSet.FromDto(dto, instanceId);

        /// <inheritdoc />
        public ManagedGrowthSet Create(string instanceId) => new ManagedGrowthSet(instanceId);
    }
}
