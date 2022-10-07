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
        
        var builder = ImmutableDictionary.CreateBuilder<GlobalShardId, ImmutableArray<NodeInfo>>();

        // keep allocations to a minimum with ArrayPools
        // first get a sorted copy of NodeInfo array
        var orderedNodes = ArrayPool<NodeInfo>.Shared.Rent(nodes.Length);
        nodes.CopyTo(orderedNodes);
        Array.Sort(orderedNodes, 0, nodes.Length);
        
        // then calculate hashes in the same sorted order
        var orderedNodeIdHashes = ArrayPool<ulong>.Shared.Rent(nodes.Length);
        GetNodeHashes(orderedNodes, orderedNodeIdHashes, nodes.Length);
        
        foreach (var shardInfo in shardInfos)
        { 
            var shardId = shardInfo.ShardId;
            
            // 1st case - replicas that should be on all nodes (0) and those that request more replicas then nodes
            if (shardInfo.NumberOfDesiredReplicas == 0 || shardInfo.NumberOfDesiredReplicas >= nodes.Length)
            {
                builder.Add(shardId, nodes);
            }

            // 2nd case 
            // get hash and find index of a closest nodeId in the ordered array
            var shardIdHash = GetHash(shardId);
            var minIndex = FindClosestNodeIndex(shardIdHash, orderedNodeIdHashes, orderedNodes, nodes.Length);
            var nodesForShard = CopyNodeRingSection(shardInfo, orderedNodes, minIndex, nodes.Length - 1);

            builder.Add(shardId, nodesForShard);
        }
        
        // TODO: fine tune taking size into account
        
        ArrayPool<ulong>.Shared.Return(orderedNodeIdHashes);
        ArrayPool<NodeInfo>.Shared.Return(orderedNodes);
        
        return builder.ToImmutable();
    }

    private static ImmutableArray<NodeInfo> CopyNodeRingSection(ShardInfo shardInfo, NodeInfo[] orderedNodes, int startIndex, int nodeMaxIndex)
    {
        var nodesForShard = new NodeInfo[shardInfo.NumberOfDesiredReplicas];
        for (var i = 0; i < nodesForShard.Length; ++i)
        {
            nodesForShard[i] = orderedNodes[startIndex];
            startIndex = startIndex > 0 ? startIndex - 1 : nodeMaxIndex; 
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
    
    private sealed class DoNothingOnReturnHashPoolPolicy : IPooledObjectPolicy<IncrementalHash>
    {
        /// <inheritdoc />
        public IncrementalHash Create() => IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        /// <inheritdoc />
        public bool Return(IncrementalHash obj) => true;
    }
}