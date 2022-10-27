using MessagePack;

namespace Nyris.Crdt.AspNetExampleV2;

[Union(0, typeof(Contains))]
[Union(1, typeof(Remove))]
[Union(2, typeof(Add))]
public abstract record SetOperation;

[MessagePackObject]
public sealed record Contains([property: Key(0)] double Value) : SetOperation; 

[MessagePackObject]
public sealed record Remove([property: Key(0)] double Value) : SetOperation;

[MessagePackObject]
public sealed record Add([property: Key(0)] double Value) : SetOperation;

// [Union(0, typeof(Bool))]
// public abstract record SetOperationResult;

[MessagePackObject]
public sealed record Bool([property: Key(0)] bool Success); // : SetOperationResult;