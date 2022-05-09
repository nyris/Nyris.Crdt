using System;
using System.Security.Cryptography;
using System.Text;
using Nyris.Crdt.Model;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record ImageInfo([property: ProtoMember(1)] Uri DownloadUrl,
        [property: ProtoMember(2)] string ImageId) : IHashable
    {
        /// <inheritdoc />
        public ReadOnlySpan<byte> CalculateHash()
        {
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            md5.AppendData(Encoding.UTF8.GetBytes(DownloadUrl.ToString()));
            md5.AppendData(Encoding.UTF8.GetBytes(ImageId));
            return md5.GetCurrentHash();
        }
    }
}
