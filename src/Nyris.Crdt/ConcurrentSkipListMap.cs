using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using Nyris.Crdt.Exceptions;

namespace Nyris.Crdt
{
    public sealed class ConcurrentSkipListMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Random Random = new();
        private readonly IComparer<TKey> _comparer = Comparer<TKey>.Default;
        private readonly object _heightChangeLock = new();
        
        // We start the state with a dummy Index and a dummy Node.
        // This leftmost Node does not contain a valid key-value pair 
        // Whichever height we reached, leftmost Node always has a maximum number of Indexes.
        private readonly Node _leftMostDummyNode = new(default!, default!, null);
        private volatile Index _head;
        
        private int _height = 1;
        private int _highLengthLimit = 4;
        private int _lowLengthLimit;
        private int _length;

        // Array pool is beneficial as we require to use arrays for each insertion and deletion
        // to keep track of Indexes that must be updated. 
        private readonly ArrayPool<Index> Pool = ArrayPool<Index>.Create(64, 10);

        public ConcurrentSkipListMap()
        {
            _head = new Index(_leftMostDummyNode, null, null);
        }
        
        public ConcurrentSkipListMap(IComparer<TKey> comparer)
        {
            _head = new Index(_leftMostDummyNode, null, null);
            _comparer = comparer;
        }
        
        public bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            var found = GetLastNodeLessOrEqualTo(key);

            if (Compare(found, key) != 0)
            {
                value = default;
                return false;
            }

            value = found.Value!;
            return true;
        }
        
        public bool TryRemove(TKey key, [NotNullWhen(true)] out TValue? value)
        {
            // save node with which we start. It may be overwritten during traversing Indexes 
            Node startingNode;
            
            lock (_heightChangeLock)
            {
                var head = _head;
                // Need to traverse indexes while holding lock cause during height growth another Index layer may be added,
                // linking it to current top layer. If we don't lock, this new top layer may end up pointing to a removed indexes and node  
                startingNode = TraverseAndRemoveIndexesEqualTo(key, head);
            }

            // traverse nodes
            if (!FindAndRemoveNodeWithKey(startingNode, key, out value))
            {
                return false;
            }

            DecreaseLength();
            return true;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (value is null) throw new ArgumentNullException(nameof(value));

            int height;
            Index? current;
            Node currentNode;
            var currentNodeLocked = false;

            lock (_heightChangeLock)
            {
                current = _head;
                currentNode = current.Node;
                height = _height;
            }
            
            var heightOfInserted = GetRandomHeight(height);
            
            // Index is a reference type, so rent an array
            var lockedIndexes = Pool.Rent(heightOfInserted);
            
            // bool on the other hand can be allocated on the stack. Height is logarithmic from number of items, so it will always be small  
            Span<bool> lockedIndexesBooleans = stackalloc bool[heightOfInserted];

            try
            {
                // Traverse indexes and lock the ones that we will update
                currentNode = TraverseAndLockBelowHeight(key, current, height, heightOfInserted, lockedIndexesBooleans, lockedIndexes);

                // Find and lock a node that needs to be updated. This returns false if key is already present in map 
                if (Compare(currentNode, key) == 0 || !FindAndLockNode(key, ref currentNode, ref currentNodeLocked))
                {
                    return false;
                }

                // insert node with key and value
                var nextNode = currentNode.RightNode;
                var insertedNode = new Node(key, value, nextNode);
                currentNode.RightNode = insertedNode;

                // insert all new indexes 
                var newIndex = (Index?)null;
                for (var i = 0; i < heightOfInserted; ++i)
                {
                    var lockedIndex = lockedIndexes[i];
                    newIndex = new Index(insertedNode, newIndex, lockedIndex!.Right);
                    lockedIndex.RightIndex = newIndex;
                }
            }
            finally
            {
                // unlock everything
                for (var i = heightOfInserted - 1; i >= 0; --i)
                {
                    if(lockedIndexesBooleans[i]) Monitor.Exit(lockedIndexes[i]);
                    lockedIndexes[i] = null!;
                }
                if(currentNodeLocked) Monitor.Exit(currentNode);
                Pool.Return(lockedIndexes);
            }

            IncreaseLength();
            return true;
        }

        public string GetRepresentation()
        {
            var builder = new StringBuilder();
            var keyToIndex = new Dictionary<TKey, int>();
            var indexToKey = new TKey[_length];

            var i = 0;
            for (var node = _head.Node.RightNode; node is not null; node = node.RightNode)
            {
                indexToKey[i] = node.Key;
                keyToIndex[node.Key] = i++;
            }
            
            var current = _head;
            var counter = 0;
            var levels = 0;
            while (current is not null)
            {
                if (ReferenceEquals(current.Node, _leftMostDummyNode))
                {
                    builder.Append("-inf ");
                }
                else
                {
                    var nodeIndex = keyToIndex[current.Node.Key];
                    for (var j = counter; j < nodeIndex; ++j)
                    {
                        var l = indexToKey[j].ToString()!.Length;
                        for (var k = 0; k < l; ++k)
                        {
                            builder.Append('-');
                        }
                        builder.Append(' ');
                    }
                    counter = nodeIndex + 1;
                    builder.Append(current.Node.Key).Append(' ');
                }

                if (current.Right is null)
                {
                    current = _head;
                    for (var j = 0; j <= levels; ++j)
                    {
                        current = current?.Down;
                        counter = 0;
                    }
                    if (current is null)
                    {
                        builder.AppendLine();
                        break;
                    }

                    builder.AppendLine();
                    ++levels;
                    continue;
                }
                
                current = current.Right;
            }

            builder.Append("-inf ");
            for (var node = _head.Node.RightNode; node is not null; node = node.RightNode)
            {
                builder.Append(node.Key).Append(' ');
            }
            
            return builder.ToString();
        }

        public Enumerator GetEnumerator() => new(_leftMostDummyNode, _leftMostDummyNode, _comparer);
        
        public Enumerator WithinRange(TKey fromInclusive, TKey toExclusive)
        {
            Debug.Assert(_comparer.Compare(fromInclusive, toExclusive) < 0);
            var start = GetLastNodeLessThen(fromInclusive);
            return new Enumerator(start, toExclusive, _leftMostDummyNode, _comparer);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private T? MoveRightWhileNodesKeyIsLess<T>(ref T current, TKey key) where T : INode<T>
        {
            // We need local copy of currentRight because loop checks on two conditions (not null and compare).
            // We can't use current.Right in those conditions, because Right pointer might change in between checks
            var currentRight = current.Right;
            for(; currentRight is not null && currentRight.CompareTo(key, _leftMostDummyNode, _comparer) < 0; currentRight = current.Right)
            {
                current = currentRight;
            }

            return currentRight;
        }
        
        private void MoveRightWhileNodesKeyIsLessOrEqual<T>(ref T current, TKey key) where T : INode<T>
        {
            // We need local copy of currentRight because loop checks on two conditions (not null and compare).
            // We can't use current.Right in those conditions, because Right pointer might change in between checks
            
            for(var currentRight = current.Right; currentRight is not null && currentRight.CompareTo(key, _leftMostDummyNode, _comparer) <= 0; currentRight = current.Right)
            {
                current = currentRight;
            }
        }

        private Node GetLastNodeLessOrEqualTo(TKey key)
        {
            // Traverse indexes.
            var current = _head;
            var currentNode = current.Node;
            
            while (current is not null)
            {
                // Move to right until end or index with greater key. 
                MoveRightWhileNodesKeyIsLessOrEqual(ref current, key);

                // if there are no Down pointer, we already traversed every layer, only Nodes themselves are left
                if (current.Down is null)
                {
                    currentNode = current.Node;
                    break;
                }
                
                current = current.Down;
            }
            
            // traverse nodes
            MoveRightWhileNodesKeyIsLessOrEqual(ref currentNode, key);
            return currentNode;
        }
        
        private Node GetLastNodeLessThen(TKey key)
        {
            // Traverse indexes.
            var current = _head;
            var currentNode = current.Node;

            while (current is not null)
            {
                // Move to right until end or index with greater key. 
                MoveRightWhileNodesKeyIsLess(ref current, key);

                // if there are no Down pointer, we already traversed every layer, only Nodes themselves are left
                if (current.Down is null)
                {
                    currentNode = current.Node;
                    break;
                }
                
                current = current.Down;
            }
            
            // traverse nodes
            MoveRightWhileNodesKeyIsLess(ref currentNode, key);
            return currentNode;
        }
        
        private Node TraverseAndRemoveIndexesEqualTo(TKey key, Index current)
        {
            while (current is not null)
            {
                var currentRight = MoveRightWhileNodesKeyIsLess(ref current, key);

                // found index that needs removal -> currentRight
                if (currentRight is not null && Compare(currentRight.Node, key) == 0)
                {
                    lock (current)
                    lock (currentRight)
                    {
                        // Current got updated before we could lock it (could have been deleted or another index could have been inserted after)
                        // Unlock and continue search from current 
                        if (!ReferenceEquals(current.Right, currentRight)) continue;

                        // remove currentRight by overwriting current.Right;
                        // Set currentRight.Right to point "left" cause it's convenient for picking up after contentious lock was acquired (see TryAdd) 
                        current.RightIndex = currentRight.Right;
                        currentRight.RightIndex = current;
                    }
                }

                // if there are no Down pointer, we already traversed every layer, only Nodes themselves are left
                if (current.Down is null)
                {
                    return current.Node;
                }

                current = current.Down;
            }

            throw new AssumptionsViolatedException("Should not be reachable - it should always be possible to reach an Index with no Down pointer");
        }

        private bool FindAndRemoveNodeWithKey(Node currentNode, TKey key, [NotNullWhen(true)] out TValue? value)
        {
            while (true)
            {
                var currentNodeRight = MoveRightWhileNodesKeyIsLess(ref currentNode, key);

                if (currentNodeRight is not null && Compare(currentNodeRight, key) == 0)
                {
                    lock (currentNode)
                    lock (currentNodeRight)
                    {
                        // Current got updated before we could lock it (could have been deleted or another index could have been inserted after)
                        // Or currentRight was deleted by other thread
                        // Unlock and continue search from current
                        if (!ReferenceEquals(currentNode.RightNode, currentNodeRight)) continue;

                        // remove currentRight by overwriting current.Right;
                        // Set currentRight.Right to point "left" cause it's convenient for picking up after contentious lock was acquired (see TryAdd) 
                        currentNode.RightNode = currentNodeRight.RightNode;
                        currentNodeRight.RightNode = currentNode;
                        value = currentNodeRight.Value!;

                        return true;
                    }
                }

                value = default;
                return false;
            }
        }

        private bool FindAndLockNode(TKey key, ref Node currentNode, ref bool currentNodeLocked)
        {
            while (true)
            {
                MoveRightWhileNodesKeyIsLessOrEqual(ref currentNode, key);

                // If key already present, return immediately to avoid excessive locking 
                if (Compare(currentNode, key) == 0)
                {
                    return false;
                }

                Monitor.Enter(currentNode, ref currentNodeLocked);

                // Check that while we waited for lock condition has not been invalidated.
                if (currentNode.RightNode is null || Compare(currentNode.RightNode, key) > 0)
                {
                    return true;
                }
 
                // If condition is not valid anymore (currentNode.RightNode is not null and its key is less or equal to the one being inserted)
                // then try to unlock and search again. 
                if (currentNodeLocked)
                {
                    Monitor.Exit(currentNode);
                    currentNodeLocked = false;
                }
            }
        }

        
        private Node TraverseAndLockBelowHeight(TKey key,
                                                Index current,
                                                int currentHeight,
                                                int heightOfInserted,
                                                Span<bool> lockedIndexesBooleans,
                                                Index[] lockedIndexes)
        {
            while (current is not null)
            {
                MoveRightWhileNodesKeyIsLessOrEqual(ref current, key);
                
                // early termination to avoid excessive locking (insert fails if key already exists)
                if (Compare(current, key) == 0) return current.Node;
                
                // using short-circuit evaluation of &&
                if (currentHeight <= heightOfInserted && !TryLockAndSave(key, current, currentHeight - 1, lockedIndexesBooleans, lockedIndexes))
                {
                    continue;
                }
                
                // If Down is null, it means we reached the bottom-most Index layer and can start iterating the Nodes 
                if (current.Down is null)
                {
                    return current.Node;
                }

                current = current.Down;
                --currentHeight;
            }
            
            throw new AssumptionsViolatedException("Should not be reachable - it should always be possible to reach an Index with no Down pointer");
        }

        private bool TryLockAndSave(TKey key, Index current, int i, Span<bool> lockedIndexesBooleans, Index[] lockedIndexes)
        {
            // if we are at an index, that is less or equal in height then one of new Indexes we will insert, lock it
            // (because this Index will need to be updated, by changing its Right pointer)
            Monitor.Enter(current, ref lockedIndexesBooleans[i]);
            lockedIndexes[i] = current;

            // While we waited for the lock, another thread might have inserted something ahead.
            // Check that condition for stopping still holds (current.Right is null or greater then key) and resume the loop otherwise.
            // This behaviour is utilized by TryRemove in an interesting way - when removing an Index, its Right pointer is
            // changed to point backwards, to a previous/left Index. This "if" condition is then triggered and we "circle back"  
            if (current.Right is null || Compare(current.Right, key) > 0)
            {
                return true;
            }

            // Release lock. Note that we can't wrap this in try-catch here because if previous if is triggered we want to return
            // without unlocking. We also can't outsource this particular unlocking to an outer try-catch because lockedIndexes[i]
            // will be overwritten 
            if (lockedIndexesBooleans[i])
            {
                Monitor.Exit(current);
                lockedIndexesBooleans[i] = false;
            }

            return false;
        }
        
        private void DecreaseLength()
        {
            if (Interlocked.Decrement(ref _length) > _lowLengthLimit) return;

            lock (_heightChangeLock)
            {
                // check if condition still holds. Prevents multiple decreases due to lock contention
                if (_length > _lowLengthLimit) return;
                
                // by simply forgetting about the pointer to current head, the entire top layer can be garbage collected eventually
                _head = _head.Down!;
                
                --_height;
                Debug.Assert(_height >= 1);
                
                _lowLengthLimit = _height == 1 ? 0 : (1 << (_height - 1));
                _highLengthLimit = 1 << (_height + 1);
            }
        }

        private void IncreaseLength()
        {
            if (Interlocked.Increment(ref _length) < _highLengthLimit) return;
            
            lock(_heightChangeLock)
            {
                // check if condition still holds. Prevents multiple increases due to lock contention
                if (_length < _highLengthLimit) return; 
                
                // move through top layer, each time flip a coin. 50% of the time we add another Index on top.
                // one exception - first one is a dummy, so we always create new Index for it 
                var currentInNewTopLayer = new Index(_head.Node, _head, null);

                // save first Index from old top layer, then overwrite _head   
                var currentInOldTopLayer = _head.Right;
                _head = currentInNewTopLayer;
                
                while (currentInOldTopLayer != null)
                {
                    if (CoinFlip())
                    {
                        var nextInNewTopLayer = new Index(currentInOldTopLayer.Node, currentInOldTopLayer, null);
                        currentInNewTopLayer.RightIndex = nextInNewTopLayer;
                        currentInNewTopLayer = nextInNewTopLayer;
                    }

                    currentInOldTopLayer = currentInOldTopLayer.Right;
                }

                ++_height;
                _highLengthLimit = 1 << (_height + 1);
                _lowLengthLimit = 1 << (_height - 1);
            }
        }

        private static int GetRandomHeight(int maxHeight)
        {
            for (var height = 0; height <= maxHeight; ++height)
            {
                if (CoinFlip()) return height;
            }

            return maxHeight;
        }
        
        private static bool CoinFlip() => (Random.Next() & 1) == 0;
        
        private int Compare<T>(T node, TKey key) where T : INode<T> => node.CompareTo(key, _leftMostDummyNode, _comparer);

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>
        {
            private readonly Node _leftMostDummyNode;
            private readonly IComparer<TKey> _comparer;
            private readonly TKey? _lessThen;
            private readonly bool _upperLimitExists;
            private Node _currentNode;

            /// <summary>
            /// Enumerate all key-value pairs from linked list that have keys strictly less then <see cref="lessThen"/> 
            /// </summary>
            /// <param name="nodeBeforeFirst"></param>
            /// <param name="lessThen"></param>
            /// <param name="leftMostDummyNode"></param>
            /// <param name="comparer"></param>
            public Enumerator(Node nodeBeforeFirst, TKey lessThen, Node leftMostDummyNode, IComparer<TKey> comparer)
            {
                _currentNode = nodeBeforeFirst;
                _comparer = comparer;
                _lessThen = lessThen;
                _leftMostDummyNode = leftMostDummyNode;
                _upperLimitExists = true;
            }
            
            /// <summary>
            /// Enumerate all key-value pairs from linked list
            /// </summary>
            /// <param name="nodeBeforeFirst"></param>
            /// <param name="leftMostDummyNode"></param>
            /// <param name="comparer"></param>
            public Enumerator(Node nodeBeforeFirst, Node leftMostDummyNode, IComparer<TKey> comparer)
            {
                _currentNode = nodeBeforeFirst;
                _comparer = comparer;
                _leftMostDummyNode = leftMostDummyNode;
                _lessThen = default;
                _upperLimitExists = false;
            }

            public bool MoveNext()
            {
                if (_currentNode.RightNode is null) return false;
                
                var current = _currentNode;
                
                // Move to right until next node is greater then last key we yielded. This is usually just one step, however 
                // a loop is necessary due to how deletions are implemented (Right pointer looped back to a previous node)
                do
                {
                    current = current.RightNode;
                } while (current is not null && Compare(current, _currentNode) <= 0);

                // we want to yield only keys that are less then _lessThen.
                // Which means that if current key is greater or equal to _lessThen, we stop
                if (current is null || (_upperLimitExists && Compare(current, _lessThen) >= 0)) return false;

                _currentNode = current;
                return true;
            }

            public void Reset() {}

            public KeyValuePair<TKey, TValue> Current => new(_currentNode.Key, _currentNode.Value);
            object IEnumerator.Current => Current;

            public void Dispose()
            {
                // nothing to dispose
            }
            
            public Enumerator GetEnumerator() => this;
            IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private int Compare(Node node, TKey? key)
            {
                if (ReferenceEquals(node, _leftMostDummyNode)) return -1;
                return _comparer.Compare(node.Key, key);
            }
            
            private int Compare(Node x, Node y)
            {
                if (ReferenceEquals(x, _leftMostDummyNode)) return -1;
                if (ReferenceEquals(y, _leftMostDummyNode)) return 1;
                return _comparer.Compare(x.Key, y.Key);
            }
        }
        
        public sealed class Node : INode<Node>
        {
            public volatile Node? RightNode;
            public readonly TKey Key;
            public readonly TValue Value;

            public Node(TKey key, TValue value, Node? rightNode)
            {
                Key = key;
                Value = value;
                RightNode = rightNode;
            }

            public Node? Right => RightNode;
            public int CompareTo(TKey key, Node leftMostDummyNode, IComparer<TKey> comparer)
            {
                // the intuition is - if current Node is leftMostDummyNode, we always want to move away from it,
                // so we say it's less then whichever node is to the right
                // we can't skip this because Key of the leftMostNode has default value (think of value types like int - default will be 0)
                if (ReferenceEquals(this, leftMostDummyNode)) return -1;
                return comparer.Compare(Key, key);
            }
        }

        private interface INode<out T>
        {
            T? Right { get; }

            int CompareTo(TKey key, Node leftMostDummyNode, IComparer<TKey> comparer);
        }
        
        private sealed class Index : INode<Index>
        {
            public volatile Index? RightIndex;
            public readonly Index? Down;
            public readonly Node Node;

            public Index(Node node, Index? down, Index? right)
            {
                Down = down;
                RightIndex = right;
                Node = node;
            }

            public Index? Right => RightIndex;
            public int CompareTo(TKey key, Node leftMostDummyNode, IComparer<TKey> comparer)
            {
                // the intuition is - if current Index is leftMostIndex, we always want to move away from it,
                // so we say it's less then whichever node is to the right 
                // we can't skip this because Key of the leftMostNode has default value (think of value types like int - default will be 0) 
                if (ReferenceEquals(Node, leftMostDummyNode)) return -1;
                return comparer.Compare(Node.Key, key);
            }
        }
    }
}
