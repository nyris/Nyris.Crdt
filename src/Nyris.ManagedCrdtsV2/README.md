# Thoughts on what needs to change in version 2

1. Management of partially replicated and sharded collections should be merged with Context. 
2. Propagation and consistency should be redesigned with delta crdts in mind.
3. Propagation that is awaited should not be based on a queue - too many deadlock problems.

### Propagation scenarios:
 
- Local state change is fine, propagation to other nodes is fine to do later 
- Change needs to be propagated to (some) other nodes, only then operation is successful

Instead of using a queue, abstract propagation from crdt completely. Simply pass an interface inside that does everything:
 
IPropagationStrategy<in TDelta> {
    Task PropagateAsync(TDelta dto, CancellationToken = default);
    void MaybePropagateLater(TDelta dto);
}

Implementation can buffer those deltas and send them out either when buffer is full 

### Consistency

1. Delta CRDTs can be accept some form of 'timestamp', which helps skip deltas that other crdt has already seen. 

Sidenote: naive solution for ObservedRemoveSet is to send version vector as "timestamp". However, it is only enough to filter
'added' deltas, but is not enough information to tell which 'removed' deltas to send and thus the only option is to send everything.
A more complete solution is to send (1) version context and (2) either all existing or all removed dots (as ranges). With this
there is no ambiguity and if everything matches, set can respond with no deltas.

2. Hash based comparisons are still valid. Depending on the level of paranoia, hash can be computed in multiple ways.
For ORSet it should be sufficient to get hash of VersionContext + count of existing dots. However, perhaps there is a 
situation that I do not yet see that would justify keeping a Merkle Tree for all items as well.




### unsorted

 - Since I need factories anyway, there is no reason to allow creation of ManagedCRDT with 'new' at all 
 - If I am always using factories to create new ManagedCRDT, context may work behind the scenes
 - Context can be striped of it's job entirely. There can be one 'entry' point which contains Dictionary with CRDTs
and one router, which knows which should be local and which should be send where

Let's say I have this:

ManagedCRDT {
    private Router _router;

    Task AddAsync(item) {
        if (_router.IsLocal(InstanceId) ) {
            ... update local state ...
        }
        else _router.SendAsync(new Operation(item));
    }
}

How do I know that, between IsLocal and actually changing the state, distribution did not change?
I.e. - how to lock router and not run redistributions while AddAsync is still running 

Options: 

1. Get an IDisposable 'transaction token' from router. 
Inside router - ConcurrentDictionary<InstanceId, ReaderWriterLockSlim>

1.1 Creating new token each time is additional allocation. Can I use object pool? 
1.2 Problem with object pool is returning token back. 
1.3 If I use struct for token, it's a stack allocation. 
However, implementing an IDisposable struct is an anti-pattern. 
1.4 ref structs support using statement by implementing Dispose directly. 
However, making token a ref struct makes it impossible to use in async method. 
Given that part of the method sends operation to another node via network AND we need to dispose after it, this is not an option


2. There is a problem with separating Router and CrdtContext:

Flow for propagation:
ManagedORSet.Add() -> _router.Propagate(delta) -> GrpcService -> CrdtContext.Merge() -> ManagedORSet.Merge() -> propagate?

Flow for rerouting (for partially replicated):
ManagedORSet.Add() -> _router.IsLocal => false -> _router.Reroute(Operation) -> GrpcService -> CrdtContext.Apply() -> ManagedORSet.Apply() -> _propagationStrategy.Propagate() -> ...

How router knows what to distribute? 

 - Needs NodeSet
 - Needs CrdtInfos { InstanceId, StorageSize, Config { Replication, Strategies }}

Can't pass NodeSet into Router, because it's a circular dependency. Does NodeSet need to be Managed though? What if it's a regular CRDT set inside Router
How does CrdtInfos gets updated? 
 - Most of it is actually immutable, so maybe it can be separated
 - But important part to the router is not immutable - StorageSize
 - StorageSize inherently must come from inside each CRDT - only they know their own storage requirements
 - Router already is inside ManagedCRDT, so it can expose method for updating StorageSize 
 - No need to follow same pattern for those two - there can be a pre-generated grpc method for transferring deltas (no need for operations, configs are always fully replicated)
 - How does nodes get removed? Currently ChannelManager update NodeSet directly, but now Router depends on channel manager
 - Does router has to depend on channel manager? What if there is a CrdtDistributor with NodeSet and CrdtInfos and Router that depends on it


## Future improvements:

 - In addition to set and maps based on ObservedRemove principle, there is an ObservedRemove Array, also bounded in space and supporting delta updates. 
Its description can be found in paper [DSON: JSON CRDT Using Delta-Mutations For Document Stores](https://iditkeidar.com/wp-content/uploads/2021/12/JSON_CRDTs___VLDB.pdf)
In the same paper authors show how to combine it with ORSet to get a full JSON support based on delta CRDTs. Though remember that this particular paper is under non-commercial license.  

 - In the spirit of extendability, grpc exceptions should be wrapped in a project defined ones, which can then be caught and handled in internal services.
 - Inverse data structure used ObservedRemoveSet and ObservedRemoveMap currently using a SortedList, but it would be better to write a custom tree-based structure. 
See [VersionedItemList](../../src/Nyris.Crdt/Model/VersionedItemList.cs) for details   
 - Lots of allocations thanks to logging, great opportunity for [high-performance logging with source generators](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
 - 