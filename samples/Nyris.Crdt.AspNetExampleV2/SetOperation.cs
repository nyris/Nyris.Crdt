using MessagePack;
using Nyris.Crdt.Serialization.Abstractions;

namespace Nyris.Crdt.AspNetExampleV2;


/// <summary>
/// If you need custom types be usable within Managed Crdt, they need to be serializable with <see cref="ISerializer"/>
/// If you are using default <see cref="Nyris.Crdt.Serialization.MessagePack.MessagePackSerializer"/>, then its enough
/// to annotate your types with Message Pack attributes (see https://github.com/neuecc/MessagePack-CSharp)
/// </summary>
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