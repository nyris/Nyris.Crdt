### Everything CRDTs

So far there are two parts to the repo:

1. [Nyris.Crdt](src/Nyris.Crdt) - this project contains interfaces and some implementations of CRDT data types.
2. [Nyris.Crdt.Distributed](src/Nyris.Crdt.Distributed) and 
   [Nyris.Crdt.Distributed.SourceGenerators](src/Nyris.Crdt.Distributed.SourceGenerators) contain logic for 
   easy and automatic use of any CRDT data type in a distributed manner. 
   That is, so that a type can be synced across multiple servers wit guarantee of strong eventual consistency.      