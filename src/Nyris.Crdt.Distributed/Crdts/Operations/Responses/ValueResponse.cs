using ProtoBuf;

namespace Nyris.Crdt.Distributed.Crdts.Operations.Responses;

[ProtoContract(SkipConstructor = true)]
public record ValueResponse<TValue>([property: ProtoMember(1)] TValue Value) : RegistryOperationResponse;
