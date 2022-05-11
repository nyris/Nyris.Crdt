using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Nyris.Crdt.AspNetExample.Mongo;

public sealed class ImageDataSetDocument : ImageDocument
{
    [BsonElement("iid")]
    public string ImageId { get; set; }

    [BsonElement("d")]
    public Uri DownloadUrl { get; set; }
}
