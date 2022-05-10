namespace Nyris.Crdt.AspNetExample.Mongo;

public sealed class MongoConfiguration
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/nyris";
    public string Database { get; set; } = "nyris";
    public string Collection { get; set; } = "distributed-prototype-test";
    public string ClientConfigurations { get; set; } = "client_configurations";
}
