using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.ObjectPool;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.ManagedCrdtsV2;


[SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
internal sealed class DistributionStrategy : IDistributionStrategy
{
    private static readonly ObjectPool<IncrementalHash> Pool =
        new DefaultObjectPool<IncrementalHash>(new DoNothingOnReturnHashPoolPolicy());

    private static readonly ObjectPool<Dictionary<int, int>> DictionaryPool =
        new DefaultObjectPool<Dictionary<int, int>>(new HashSetPoolPolicy());

    // Dict<int, int> mean - in a sorted array of nodes, which index (key) have how many shards (value) 
    private readonly Dictionary<InstanceId, Dictionary<int, int>> _instanceNodeIndexes = new();

    public ImmutableDictionary<GlobalShardId, ImmutableArray<NodeInfo>> Distribute(ImmutableArray<ShardInfo> shardInfos, ImmutableArray<NodeInfo> nodes)
    {
        // Idea for how this works:
        // 1. All replicas that should be on all nodes just get a copy of all nodes
        // 2. For all other replicas - find 'closest' nodeId and assign copies on {NumberOfDesiredReplicas} next nodes in ring
        //    What is 'closest'?  Simple - hash ShardId and NodeId and compare them.
        //    What is distance between 2 hashes?  We can define it as the number of different bits.
        //
        //    Full MD5 hash is 128 bits, but it's inconvenient to work with, so we take first 64 bits as ulong.
        //    Once we have 2 ulongs, XOR them and count number of ones in the result bit representation.
        //    The result is a number between 0 and 64, where 64 means that _every_ bit is different and 0
        //    means that all bits are the same.
        //
        //    We assign shard to a node with smallest distance (and smallest nodeId if smallest distance is not unique)
        //    We then take next {NumberOfDesiredReplicas} nodes from sorted nodeIds list
        // 3. Finally, we need to do additional fine-tuning that takes into account sizes
        //    and spreads shards of same crdts to different nodes:
        // 3.1  Ensure that different shards of the same InstanceId are on different nodes 
        
        var builder = ImmutableDictionary.CreateBuilder<GlobalShardId, ImmutableArray<NodeInfo>>();

        // keep allocations to a minimum with ArrayPools
        // first get a sorted copy of NodeInfo array
        var length = nodes.Length;
        var orderedNodes = ArrayPool<NodeInfo>.Shared.Rent(length);
        nodes.CopyTo(orderedNodes);
        Array.Sort(orderedNodes, 0, length);
        
        // then calculate hashes in the same sorted order
        var orderedNodeIdHashes = ArrayPool<ulong>.Shared.Rent(length);
        GetNodeHashes(orderedNodes, orderedNodeIdHashes, length);
        
        foreach (var shardInfo in shardInfos)
        { 
            var shardId = shardInfo.ShardId;
            
            // 1st phase - replicas that should be on all nodes (0) and those that request more replicas then nodes
            if (shardInfo.NumberOfDesiredReplicas == 0 || shardInfo.NumberOfDesiredReplicas >= length)
            {
                builder.Add(shardId, nodes);
                continue;
            }

            // 2nd phase 
            // get hash and find index of a closest nodeId in the ordered array
            var shardIdHash = GetHash(shardId);
            var minIndex = FindClosestNodeIndex(shardIdHash, orderedNodeIdHashes, orderedNodes, length);
            
            // 3.1 phase - we want to spread different shards of a single InstanceId to different nodes if possible.
            minIndex = EnsureShardsAreSpreadAcrossNodes(shardId, minIndex, length);
            // 3.2: TODO: how to take size into account?
            
            var nodesForShard = CopyNodeRingSection(shardInfo, orderedNodes, minIndex, length - 1);
            builder.Add(shardId, nodesForShard);
        }
        
        // return all rented objects to pools
        ArrayPool<ulong>.Shared.Return(orderedNodeIdHashes);
        ArrayPool<NodeInfo>.Shared.Return(orderedNodes);

        foreach (var dictionary in _instanceNodeIndexes.Values) DictionaryPool.Return(dictionary);
        _instanceNodeIndexes.Clear();
        
        return builder.ToImmutable();
    }

    private int EnsureShardsAreSpreadAcrossNodes(GlobalShardId shardId, int minIndex, int length)
    {
        if (!_instanceNodeIndexes.TryGetValue(shardId.InstanceId, out var nodeIndexToNumberOfShards))
        {
            nodeIndexToNumberOfShards = _instanceNodeIndexes[shardId.InstanceId] = DictionaryPool.Get();
        }

        var newMinIndex = minIndex;
        // if this node already contain at least 1 another shard with same InstanceId
        if (nodeIndexToNumberOfShards.ContainsKey(minIndex))
        {
            // save the number of shards which the found node already has
            var minCount = nodeIndexToNumberOfShards[minIndex];

            // iterate over all node indexes. For example, minIndex=2 and we have 5 nodes - so indexes are [0,1,2,3,4]
            // We move upwards i : 2 -> 3 -> 4 -> 0 -> 1
            for (var j = 0; j < length; ++j)
            {
                var i = (minIndex + j) % length;
                // if i-th node contains strictly less shards, save it. 
                var ithShardCount = nodeIndexToNumberOfShards.GetValueOrDefault(i);
                if (ithShardCount < minCount)
                {
                    newMinIndex = i;
                    minCount = ithShardCount;
                }
            }
            // after the loop, minIndex contains a closest node index to originally found in 2nd phase, which contains the least shards of same InstanceId
        }

        if (nodeIndexToNumberOfShards.ContainsKey(newMinIndex))
        {
            nodeIndexToNumberOfShards[newMinIndex] += 1;
        }
        else
        {
            nodeIndexToNumberOfShards[newMinIndex] = 1;
        }

        return newMinIndex;
    }

    private static ImmutableArray<NodeInfo> CopyNodeRingSection(ShardInfo shardInfo, NodeInfo[] orderedNodes, int startIndex, int nodeMaxIndex)
    {
        var nodesForShard = new NodeInfo[shardInfo.NumberOfDesiredReplicas];
        for (var i = 0; i < nodesForShard.Length; ++i)
        {
            nodesForShard[i] = orderedNodes[startIndex];
            startIndex = startIndex < nodeMaxIndex ? startIndex + 1 : 0; 
        }

        return Unsafe.As<NodeInfo[], ImmutableArray<NodeInfo>>(ref nodesForShard);
    }

    private static int FindClosestNodeIndex(ulong shardIdHash, ulong[] orderedNodeIdHashes, NodeInfo[] orderedNodes, int length)
    {
        var min = 64;
        var minIndex = 0;
        for (var i = 0; i < length; ++i)
        {
            var distance = BitOperations.PopCount(shardIdHash ^ orderedNodeIdHashes[i]);
            // either distance is strictly less then saved min, or its equal AND i-th NodeId is less then current min
            if (distance < min || (distance == min && orderedNodes[i] < orderedNodes[minIndex]))
            {
                min = distance;
                minIndex = i;
            }
        }

        return minIndex;
    }

    private static ulong GetHash(in GlobalShardId shardId)
    {
        var hash = Pool.Get();
        hash.AppendData(MemoryMarshal.AsBytes(shardId.InstanceId.AsReadOnlySpan));
        hash.AppendData(BitConverter.GetBytes(shardId.ShardId.AsUint));
        var hashedShardId = BitConverter.ToUInt64(hash.GetHashAndReset());
        Pool.Return(hash);
        return hashedShardId;
    }
    
    private static void GetNodeHashes(NodeInfo[] nodes, ulong[] nodeIdHashes, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var hash = Pool.Get();
            hash.AppendData(MemoryMarshal.AsBytes(nodes[i].Id.AsReadOnlySpan));
            var hashBytes = hash.GetHashAndReset();
            Pool.Return(hash);
            nodeIdHashes[i] = BitConverter.ToUInt64(hashBytes);
        }
    }

    private sealed class HashSetPoolPolicy : IPooledObjectPolicy<Dictionary<int, int>>
    {
        public Dictionary<int, int> Create() => new(10);

        public bool Return(Dictionary<int, int> obj)
        {
            obj.Clear();
            return true;
        }
    }

    private sealed class DoNothingOnReturnHashPoolPolicy : IPooledObjectPolicy<IncrementalHash>
    {
        /// <inheritdoc />
        public IncrementalHash Create() => IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        /// <inheritdoc />
        public bool Return(IncrementalHash obj) => true;
    }
}