using System;
using System.Text;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Extensions.Guids;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Represents an InstanceId structure, which encapsulates string and allows to explicitly separate ids for different entities
    /// </summary>
    [StronglyTypedId(backingType: StronglyTypedIdBackingType.String, jsonConverter: StronglyTypedIdJsonConverter.SystemTextJson)]
    [ProtoContract]
    public readonly partial struct InstanceId: IHashable
    {
        /// <summary>
        /// Converts string into InstanceId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static InstanceId FromString(string id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static InstanceId New() => new(ShortGuid.Encode(Guid.NewGuid()));

        [ProtoMember(1)]
        private string _id => Value;

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => Encoding.Default.GetBytes(_id);
    }
}