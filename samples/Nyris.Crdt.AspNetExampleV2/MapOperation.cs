using MessagePack;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExampleV2;

[Union(0, typeof(AddOperation))]
[Union(1, typeof(RemoveOperation))]
[Union(2, typeof(FindOperation))]
public abstract record MapOperation;
[MessagePackObject] public sealed record AddOperation([property: Key(0)] ImageId Id, [property: Key(1)] float[] Vector) : MapOperation;
[MessagePackObject] public sealed record RemoveOperation([property: Key(0)] ImageId Id) : MapOperation;
[MessagePackObject] public sealed record FindOperation([property: Key(0)] float[] Vector) : MapOperation;


[Union(0, typeof(RemovalResult))]
[Union(1, typeof(Empty))]
[Union(2, typeof(SearchResult))]
public abstract record MapOperationResult;

[MessagePackObject] 
public sealed record SearchResult([property: Key(0)] ImageId Id, [property: Key(1)] float DotProduct) : MapOperationResult;

[MessagePackObject] public sealed record RemovalResult([property: Key(0)] bool Success) : MapOperationResult;

[MessagePackObject] public sealed record Empty : MapOperationResult;