using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Nyris.Crdt.AspNetExample.Mongo;

[BsonKnownTypes(typeof(ImageDataSetDocument), typeof(ImageDeletedDocument))]
public abstract class ImageDocument
{
    [BsonElement("uuid")]
    public Guid ImageUuid { get; set; }

    [BsonElement("ind")]
    public Guid IndexId { get; set; }

    [BsonElement("t")]
    public DateTime EventTime { get; set; }
}
