using Nyris.Crdt.Model;
using ProtoBuf;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Nyris.Crdt.AspNetExample
{
    [ProtoContract(SkipConstructor = true)]
    public record User([property: ProtoMember(1)] Guid Id, [property: ProtoMember(2)] string FirstName,
        [property: ProtoMember(3)] string LastName) : IHashable
    {
        public ReadOnlySpan<byte> CalculateHash()
        {
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

            md5.AppendData(Encoding.UTF8.GetBytes(FirstName));
            md5.AppendData(Encoding.UTF8.GetBytes(LastName));

            return md5.GetCurrentHash();
        }
    }
}
