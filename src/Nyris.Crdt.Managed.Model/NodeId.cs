using System.Runtime.InteropServices;

namespace Nyris.Crdt.Managed.Model;

[StronglyTypedId(backingType: StronglyTypedIdBackingType.String,
    jsonConverter: StronglyTypedIdJsonConverter.NewtonsoftJson | StronglyTypedIdJsonConverter.SystemTextJson)]
public readonly partial struct NodeId
{
    public static NodeId FromString(string value) => new(value);
    public static NodeId GenerateNew() => new(Guid.NewGuid().ToString("N"));
    public ReadOnlySpan<byte> AsSpan => MemoryMarshal.AsBytes(Value.AsSpan());
}