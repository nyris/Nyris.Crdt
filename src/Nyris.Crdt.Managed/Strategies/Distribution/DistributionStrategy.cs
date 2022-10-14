using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.ObjectPool;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Managed.Strategies.Distribution;


[SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
internal sealed class DistributionStrategy : IDistributionStrategy
{
    private static readonly ObjectPool<IncrementalHash> Pool =
        new DefaultObjectPool<IncrementalHash>(new DoNothingOnReturnHashPoolPolicy());

    private static readonly ObjectPool<Dictionary<int, int>> DictionaryPool =
        new DefaultObjectPool<Dictionary<int, int>>(new DictionaryPoolPolicy());

    // Dict<int, int> mean - in a sorted array of nodes, which index (key) have how many shards (value) 
    private readonly Dictionary<InstanceId, Dictionary<int, int>> _instanceIdsToNodeIndexesToReplicaCounts = new();

    public ImmutableDictionary<ReplicaId, ImmutableArray<NodeInfo>> Distribute(in ImmutableArray<ReplicaInfo> orderedShardInfos, in ImmutableArray<NodeInfo> orderedNodes)
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
        
        var builder = ImmutableDictionary.CreateBuilder<ReplicaId, ImmutableArray<NodeInfo>>();

        // keep allocations to a minimum with ArrayPools
        // first get a sorted copy of NodeInfo array
        var length = orderedNodes.Length;
        
        // then calculate hashes in the same sorted order
        var orderedNodeIdHashes = ArrayPool<ulong>.Shared.Rent(length);
        GetNodeHashes(orderedNodes, orderedNodeIdHashes);
        
        foreach (var replicaInfo in orderedShardInfos)
        { 
            var replicaId = replicaInfo.Id;
            var requestedReplicaCount = replicaInfo.RequestedReplicaCount;
            
            // 1st phase - replicas that should be on all nodes (0) and those that request more replicas then nodes
            if (requestedReplicaCount == 0 || requestedReplicaCount >= length)
            {
                builder.Add(replicaId, orderedNodes);
                continue;
            }

            // 2nd phase 
            // get hash and find index of a closest nodeId in the ordered array
            var replicaIdHash = GetHash(replicaId);
            var minIndex = FindClosestNodeIndex(replicaIdHash, orderedNodeIdHashes, orderedNodes);
            
            // 3.1 phase - we want to spread different shards of a single InstanceId to different nodes if possible.
            minIndex = EnsureReplicasAreSpreadAcrossNodes(replicaId, minIndex, length);
            
            // 3.2: TODO: how to take size into account? Should we?
            
            var nodesForShard = CopyNodeRingSection(orderedNodes, minIndex, requestedReplicaCount);
            builder.Add(replicaId, nodesForShard);
        }
        
        // return all rented objects to pools
        ArrayPool<ulong>.Shared.Return(orderedNodeIdHashes);

        foreach (var dictionary in _instanceIdsToNodeIndexesToReplicaCounts.Values) DictionaryPool.Return(dictionary);
        _instanceIdsToNodeIndexesToReplicaCounts.Clear();
        
        return builder.ToImmutable();
    }

    private int EnsureReplicasAreSpreadAcrossNodes(in ReplicaId replicaId, int minIndex, int length)
    {
        if (!_instanceIdsToNodeIndexesToReplicaCounts.TryGetValue(replicaId.InstanceId, out var nodeIndexToReplicaCounts))
        {
            nodeIndexToReplicaCounts = _instanceIdsToNodeIndexesToReplicaCounts[replicaId.InstanceId] = DictionaryPool.Get();
        }

        var newMinIndex = minIndex;
        // if this node already contain at least 1 another shard with same InstanceId
        if (nodeIndexToReplicaCounts.ContainsKey(minIndex))
        {
            // save the number of shards which the found node already has
            var minCount = nodeIndexToReplicaCounts[minIndex];

            // iterate over all node indexes. For example, minIndex=2 and we have 5 nodes - so indexes are [0,1,2,3,4]
            // We move upwards i : 2 -> 3 -> 4 -> 0 -> 1
            for (var j = 0; j < length; ++j)
            {
                var i = (minIndex + j) % length;
                // if i-th node contains strictly less replicas, save it. 
                var replicaCountOfIthNode = nodeIndexToReplicaCounts.GetValueOrDefault(i);
                if (replicaCountOfIthNode < minCount)
                {
                    newMinIndex = i;
                    minCount = replicaCountOfIthNode;
                }
            }
            // after the loop, minIndex contains a closest node index to originally found in 2nd phase, which contains the least shards of same InstanceId
        }

        if (nodeIndexToReplicaCounts.ContainsKey(newMinIndex))
        {
            nodeIndexToReplicaCounts[newMinIndex] += 1;
        }
        else
        {
            nodeIndexToReplicaCounts[newMinIndex] = 1;
        }

        return newMinIndex;
    }

    private static ImmutableArray<NodeInfo> CopyNodeRingSection(in ImmutableArray<NodeInfo> orderedNodes, int startIndex, uint requestedReplicaCount)
    {
        var nodesForReplica = new NodeInfo[requestedReplicaCount];
        var nodeMaxIndex = orderedNodes.Length - 1;
        for (var i = 0; i < nodesForReplica.Length; ++i)
        {
            nodesForReplica[i] = orderedNodes[startIndex];
            startIndex = startIndex < nodeMaxIndex ? startIndex + 1 : 0; 
        }

        return Unsafe.As<NodeInfo[], ImmutableArray<NodeInfo>>(ref nodesForReplica);
    }

    private static int FindClosestNodeIndex(ulong replicaIdHash, ulong[] orderedNodeIdHashes, in ImmutableArray<NodeInfo> orderedNodes)
    {
        var min = 64;
        var minIndex = 0;
        for (var i = 0; i < orderedNodes.Length; ++i)
        {
            var distance = BitOperations.PopCount(replicaIdHash ^ orderedNodeIdHashes[i]);
            // either distance is strictly less then saved min, or its equal AND i-th NodeId is less then current min
            if (distance < min || (distance == min && orderedNodes[i] < orderedNodes[minIndex]))
            {
                min = distance;
                minIndex = i;
            }
        }

        return minIndex;
    }

    private static ulong GetHash(in ReplicaId id)
    {
        var hash = Pool.Get();
        hash.AppendData(id.InstanceId.AsSpan);
        hash.AppendData(BitConverter.GetBytes(id.ShardId.AsUint));
        var hashedShardId = BitConverter.ToUInt64(hash.GetHashAndReset());
        Pool.Return(hash);
        return hashedShardId;
    }
    
    private static void GetNodeHashes(in ImmutableArray<NodeInfo> nodes, ulong[] nodeIdHashes)
    {
        for (var i = 0; i < nodes.Length; i++)
        {
            var hash = Pool.Get();
            hash.AppendData(nodes[i].Id.AsSpan);
            var hashBytes = hash.GetHashAndReset();
            Pool.Return(hash);
            nodeIdHashes[i] = BitConverter.ToUInt64(hashBytes);
        }
    }

    private sealed class DictionaryPoolPolicy : IPooledObjectPolicy<Dictionary<int, int>>
    {
        public Dictionary<int, int> Create() => new(10);

        public bool Return(Dictionary<int, int>? obj)
        {
            if (obj is null) return false;
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