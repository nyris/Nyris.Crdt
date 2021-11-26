using System;
using ProtoBuf;

namespace Nyris.Crdt.AspNetExample
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record ImageInfo([property: ProtoMember(1)] Uri DownloadUrl, [property: ProtoMember(2)] string ImageId);
}