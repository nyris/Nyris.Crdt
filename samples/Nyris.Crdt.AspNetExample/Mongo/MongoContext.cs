using MongoDB.Bson;
using MongoDB.Driver;

namespace Nyris.Crdt.AspNetExample
{
    internal sealed class MongoContext
    {
        public readonly IMongoCollection<ImageDocument> Images;

        public MongoContext(IMongoClient client, MongoConfiguration configuration)
        {
            var database = client.GetDatabase(configuration.RequestsDatabase);
            Images = database.GetCollection<ImageDocument>(configuration.Collection, new MongoCollectionSettings
            {
                GuidRepresentation = GuidRepresentation.Standard
            });
        }
    }
}