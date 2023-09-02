# Conflict-Free Replicated Data Types (CRDTs)

## Open TODOs:

 - Add buffering to the PropagationService
 - Add indexes on top of callbacks (think of search indexes on top of ObservedRemoveMap with {imageUuid => vector} )
 - Optimize GetWriteReplicas and GetReadReplicas methods of Cluster 
(currently they are calculating dictionaries every time, but those can be pre-calculated)
 - Plan persistence layer

### Maybe important, but hard(er)

 - There is an implementation of a [ConcurrentSkipListMap](src/Nyris.Crdt/ConcurrentSkipListMap.cs) for Inverse data 
   structure within [ObservedRemoveCore](src/Nyris.Crdt/OptimizedObservedRemoveCore.cs) abstract class. 
   It is an open question if usage of that list is better then a simple
   SortedList, which was [used in the set before](src/Nyris.Crdt/Sets/OptimizedObservedRemoveSetV2.cs). 
   [Benchmarks](test/Nyris.Crdt.Benchmarks/ObservedRemoveSet.md) of V2 and V3 implementation does not provide a 
   definitive answer. The overall task is - how best to optimize the ObservedRemoveSet?

 - Usage of locks in ObservedRemove [map](src/Nyris.Crdt/ObservedRemoveMapV2.cs) and [set](src/Nyris.Crdt/Sets/OptimizedObservedRemoveSetV3.cs) 
   is not proven to be minimal or necessary. There are benefits in investigating which locks can be removed or changed (or which must be added) 

 - Current design of [ObservedRemoveMap](src/Nyris.Crdt/ObservedRemoveMapV2.cs) forces it to accumulate a growing number of value deltas within a single dot. 
   Each update to a key is done by calculating delta dtos produced by that update and then appending them to an array of dtos already present in the dot 
   (of the same actor). This is making an ObservedRemoveMap silently inefficient in case a lot of updates happen on the same keys (which is the common case actually).
   I don't know if there exist a design that is better, but I can't prove there is no such design either. 

 - Is there some better way of organizing solution into projects?

 - Currently metadata and other crdts are propagated separately, but they using a lot of code that potentially 
   _could_ be shared. Is there benefits in refactoring this part? How can this be done?  

 - Distribution strategy should probably take shard size into account (see [this example](src/Nyris.Crdt.Distributed/Strategies/PartialReplication/SimplePartialReplicationStrategy.cs))    

### Relatively simple and straightforward 
 - In the spirit of extendability, grpc exceptions should be wrapped in a project defined ones, which can then be caught and handled in internal services.
 - Lots of allocations thanks to logging, great opportunity for [high-performance logging with source generators](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)

### Nice to have
 - Add benchmarking to all crdts - additions and merging of ObservedRemove Set and Map are the most obvious target for optimization
 - Add benchmarking to a sample application, with tests for "real usage" performance metrics  
 - Identify other hot paths. How to best measure memory and execution time in different code parts?
 - Improve visibility. Perhaps a nice frontend with visualization of cluster metadata 




# Left from Version 1 - deprecated!!!

So far there are two parts to the repo:

1. [Nyris.Crdt](src/Nyris.Crdt) - this project contains interfaces and some implementations of CRDT data types.
2. [Nyris.Crdt.Distributed](src/Nyris.Crdt.Distributed) and 
   [Nyris.Crdt.Distributed.SourceGenerators](src/Nyris.Crdt.Distributed.SourceGenerators) contain logic for 
   easy and automatic use of any CRDT data type in a distributed manner. 
   That is, so that a type can be synced across multiple servers wit guarantee of strong eventual consistency.      

### Nyris.Crdt.Distributed

#### How to use:
   
You can find everything in one place in the [sample project](/sample/Nyris.Crdt.AspNetSample)

##### 1. Add nuget packages for Nyris.Crdt.Distributed and .Crdt.Distributed.SourceGenerator

SourceGenerator analyses CRDTs defined in your project and generates 3 things:
	- IManagedCrdtService and ManagedCrdtService - those in turn provide the basis 
	for [protobuf-net.Grpc](https://github.com/protobuf-net/protobuf-net.Grpc) to create grpc
	services (Code First grpc)
	- Extention methods for IServiceCollection

##### 2. Define a class that inherits `ManagedCrdtContext`:

```c#
internal sealed class MyContext : ManagedCrdtContext
{
}
```

##### 3. Add services to DI and request pipeline:

Call `services.AddManagedCrdts<MyContext>()` in Startup and `MapManagedCrdtService` in endpoint builder:

```c#
app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapManagedCrdtService();
});
```

##### 4. Define crdt types and factories you need

There are limitations on defining your CRDT:
1. It must inherit `ManagedCRDT` abstract type (or other abstract type that itseld inherits ManagedCRDT)
2. In order to actually be "managed" (i.e. - synced across nodes) it must be **non generic**. 
   You can define generic types as basis for concrete implementations, but using them directly 
   will result in an error.

If implementing a Dto type for your crdt, note that dto type should result in a unique nameof()
within a project. That is, even if c# allows two `class Dto` within different namespaces,
this will result in an error when using this library due to grpc methods collision.

This can play a role when, for example, trying to use same DTO type with different generic type arguments (think `SomeDto<int>` and `SomeDto<string>`). You can not use both of them together, grpc can not distinguish between them. So you will need to have a separate Dto types, like this:
`SomeDtoInt : SomeDto<int>` and `SomeDtoString : SomeDto<string>` 

Some CRDTs may need to create instances of ManagedCrdts as part of their operation. For example - this is true for registries (key-value stores), where values are themselves ManagedCRDTs (for example - PartiallyReplicatedCRDTRegistry). If such value was created on one node, it needs to be propagated to other nodes. And since that value is a ManagedCRDT it is not enough to just create it, it also needs to be added to ManagedContext.
In case CRDT needs to do that, it should implement a `ICreateAndDeleteManagedCrdtsInside` interface. Let's call CRDTs that implement that interface - container CRDTs. Then CRDTs that are created by such a "container" we will call it's "items". 

In addition to simply having an "item" CRDT, you will need an implementation of `IManagedCRDTFactory` for it.
For example:
```c#
public sealed class ImageInfoLwwCollectionFactory : IManagedCRDTFactory<ImageInfoLwwCollection, LastWriteWinsDto>
{
    /// <inheritdoc />
    public ImageInfoLwwCollection Create(InstanceId instanceId) => new ImageInfoLwwCollection(instanceId);
}
```


##### 5. Add your CRDTs to the context:

Though CRDTs are not required to be properties of ManagedCrdtContext, it is easier to keep them there.
Context will be added as Singleton in the DI, so you can easily inject it into other services.

What is required, is to call `Add` method on the ManagedCrdtContext. For example:

```c#
internal sealed class MyContext : ManagedCrdtContext
{
    public MyContext()
    {
        Add<ImageInfoCollectionsRegistry, ImageInfoCollectionsRegistry.RegistryDto>(ImageCollectionsRegistry);
    }

    public ItemInfoCollectionsRegistry ImageCollectionsRegistry { get; } = new("whatever");
}
```

Calling Add will ensure that CRDT can receive updates coming from other nodes.

###### 6. Mind the `instanceId`s 

Note that ManagedCRDTs are created with InstanceId as constructor argument.
This id is used to distinguish between CRDT instances of the same type when 
exchanging dtos with other nodes. So instanceId have to be unique within Crdt 
instances if the same type. Otherwise, it is an opaque string.


### Nyris.Crdt.AspNetExample

You can use `Nyris.Crdt.AspNetExample` project to run an example server.

#### Debug IDE

Run `Nyris.Crdt.AspNetExample` with your IDE as `node-0`

Run `node-1`:

```bash
cd samples/Node1
dotnet run --no-build
```

Run `node-2`:

```bash
cd samples/Node2
dotnet run --no-build
```

#### Docker

```bash
docker-compose up
```
