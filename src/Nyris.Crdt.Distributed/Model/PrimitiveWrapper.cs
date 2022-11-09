using System.Diagnostics.CodeAnalysis;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Protobuf-net can not use primitive types and structs as parameters in grpc methods,
    /// it requires them to be wrapped in a "message" type. This is a convenience record to do just that
    /// </summary>
    /// <param name="Value"></param>
    /// <typeparam name="T"></typeparam>
    [ProtoContract(SkipConstructor = true)]
    [SuppressMessage("Usage", "CA2225:Operator overloads have named alternates")]
    public sealed record PrimitiveWrapper<T>([property: ProtoMember(1)] T Value)
    {
        public static implicit operator T(PrimitiveWrapper<T> wrapper) => wrapper.Value;
        public static implicit operator PrimitiveWrapper<T>(T value) => new (value);
    }
}
