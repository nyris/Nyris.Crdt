using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations.Responses
{
    [ProtoContract(SkipConstructor = true)]
    public record Response<TResponse>([property: ProtoMember(1)] TResponse? Value,
        [property: ProtoMember(2)] bool Success = true,
        [property: ProtoMember(3)] string? Message = null) where TResponse : OperationResponse
    {
        public static Response<TResponse> Fail(string message) => new(null, false, message);

        public static implicit operator Response<TResponse>(TResponse value) => new(value);
    }
}