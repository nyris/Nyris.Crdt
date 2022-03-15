using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Nyris.Crdt.Distributed.Crdts.Interfaces;
using Nyris.Crdt.Distributed.Model.Converters;
using Nyris.Extensions.Guids;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    /// <summary>
    /// Encapsulates Guid
    /// </summary>
    [StronglyTypedId(jsonConverter: StronglyTypedIdJsonConverter.SystemTextJson)]
    [ProtoContract]
    public readonly partial struct ShardId : IHashable
    {
        /// <summary>
        /// Converts guid into NodeId.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ProtoConverter]
        public static ShardId FromGuid(Guid id) => new(id);

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">input – The string to convert.</param>
        /// <returns></returns>
        public static ShardId Parse(string input) => new(ShortGuid.Decode(input));

        /// <summary>
        /// Converts the string representation of an NodeId to the equivalent NodeId structure.
        /// </summary>
        /// <param name="input">A string containing the NodeId to convert</param>
        /// <param name="shardId">An NodeId instance to contain the parsed value. If the method returns true,
        /// result contains a valid NodeId. If the method returns false, result equals Empty.</param>
        /// <returns>true if the parse operation was successful; otherwise, false.</returns>
        public static bool TryParse(string input, [NotNullWhen(true)] out ShardId shardId)
        {
            if (ShortGuid.TryParse(input, out Guid guid))
            {
                shardId = FromGuid(guid);
                return true;
            }

            shardId = Empty;
            return false;
        }

        [ProtoMember(1)]
        private Guid _id => Value;

        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash() => _id.ToByteArray();
    }
}
