using System.Runtime.InteropServices;

namespace Nyris.Crdt.Managed.Model;

[StronglyTypedId(backingType: StronglyTypedIdBackingType.String,
    jsonConverter: StronglyTypedIdJsonConverter.NewtonsoftJson | StronglyTypedIdJsonConverter.SystemTextJson)]
public readonly partial struct InstanceId
{
    public static InstanceId FromString(string value) => new(value);
    public ReadOnlySpan<byte> AsSpan => MemoryMarshal.AsBytes(Value.AsSpan());
}