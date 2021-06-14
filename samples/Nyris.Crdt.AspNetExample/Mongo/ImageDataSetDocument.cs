using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageDataSetDocument : ImageDocument
    {
        [BsonElement("iid")]
        public string ImageId { get; set; }

        [BsonElement("d")]
        public Uri DownloadUri { get; set; }
    }

