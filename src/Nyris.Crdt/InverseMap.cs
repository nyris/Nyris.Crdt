using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt
{
    public interface IInverseMap<TValue>
    {
        // ##### Assumptions and considerations: #####
        //
        // 1. Implementation required to be thread safe - mutations and enumerations are are expected to be called concurrently
        //
        // 2. Fast inserts, removals and lookups (except remove range) are more important then enumerations
        //
        // 3. Enumeration should be done in reasonable time for up to 10^7 items inside the collection
        //
        // 4. EnumerateKeysOutsideRanges is expected to almost always yield either 0 or small number of keys, regardless of the total size of the collection
        //
        // 5. Insertions are done sequentially most of the time, but not always
        //    i.e. most common scenario is TryInsert(1, item1) -> TryInsert(2, item2) -> TryInsert(3, item3) -> ...
        //
        // 6. Deletions are assumed to be random
        //
        // 7. Commonly either balanced tree or a skip list is used for such a scenario
        // 
        // 8. If balanced tree is used, AVL tree is probably superior due to sequential adds
        //    (source - https://refactoringlightly.wordpress.com/2017/10/29/performance-of-avl-red-black-trees-in-java/)
        //
        // 9. Contention pressure due to additions and removal are relatively low  
        
        
        public bool TryInsert(ulong version, TValue value);
        public bool TryGet(ulong version, out TValue value);
        public bool TryRemove(ulong version, out TValue value);
        IEnumerator<Range> EnumerateVersionGapsInsideRanges(ImmutableArray<Range> ranges);
        IEnumerator<KeyValuePair<ulong, TValue>> EnumerateKeysOutsideRanges(ImmutableArray<Range> ranges);
        void RemoveRange(Range range, out IEnumerable<KeyValuePair<ulong, TValue>> removed);
    }
    
    [SuppressMessage("ReSharper", "ArrangeMethodOrOperatorBody", Justification = "Annoying")]
    public sealed class InverseMap<TValue>
    {
        

        public bool TryInsert(ulong version, TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryGet(ulong version, out TValue value)
        {
            throw new NotImplementedException();
        }

        public bool TryRemove(ulong version, out TValue value)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<Range> EnumerateVersionGapsInsideRanges(ImmutableArray<Range> ranges)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<ulong, TValue>> EnumerateKeysOutsideRanges(ImmutableArray<Range> ranges)
        {
            throw new NotImplementedException();
        }

        public void RemoveRange(Range range, out IEnumerable<KeyValuePair<ulong, TValue>> removed)
        {
            throw new NotImplementedException();
        }


    }
}
