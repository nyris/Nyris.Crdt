using System;
using System.Text;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Extensions.Guids;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Represents an NodeId structure, which encapsulates string and allows to explicitly separate ids for different entities
    /// </summary>
    [StronglyTypedId(backingType: StronglyTypedIdBackingType.String, jsonConverter: StronglyTypedIdJsonConverter.SystemTextJson)]
    [ProtoContract]
    public readonly partial struct NodeId : IHashable
    {
        /// <summary>
        /// Converts string into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static NodeId FromString(string id) => new(id);

        /// <summary>
        /// Generates new random Id.
        /// </summary>
        /// <returns></returns>
        public static NodeId New() => new(ShortGuid.Encode(Guid.NewGuid()));

        [ProtoMember(1)]
        private string _id => Value;

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => Encoding.Default.GetBytes(_id);
    }
}
