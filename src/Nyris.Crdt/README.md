
### What is CRDT?

Data type is a CRDT (Conflict-free Replicated Data Type), if set of all possible states satisfies the following constrains:
1. <b>Partial order</b>. This means at least some pairs of elements A and B from the set are ordered (i.e. A dominates B or is dominated by B)
2. Existence of <b>Join (Merge) operation</b>, defined for any pair of elements in the set and producing their Least Upper Bound
3. Whatever <b>Update</b> operations supported by the data type, state after the update must dominate state before the update.

If concurrent actors update CRDT and periodically share and merge them, then that data will be eventually consistent.

Some CRDTs are actually very simple - so much so that it doesn't feel that creating a special name is justified: 

For example:

1. Boolean flag, that is set to False at the start. 
Update consists of setting flag to True. 
Merge is an OR operation - once flag is True, every merge will produce True. 
This means that once some actor sets the flag to true, eventually all actors will set the flag to True

2. Growth set. 
Every actor can add elements to the set. No one is allowed to remove them. Merge operation is simply a Union.

However, the power of those properties is that once _any_ data type satisfies them, 
reaching consensus on it's value comes down to simply periodically sharing and merging that data between actors.

Collection of resources about CRDTs: https://crdt.tech/resources
