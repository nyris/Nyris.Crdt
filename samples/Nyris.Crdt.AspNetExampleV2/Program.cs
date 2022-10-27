using Nyris.Crdt.AspNetExampleV2;
using Nyris.Crdt.Managed.Extensions;
using Nyris.Crdt.Serialization.MessagePack;
using Nyris.Crdt.Transport.Grpc;

if ("node-3" == Environment.GetEnvironmentVariable("NODE_NAME"))
{
    await Task.Delay(TimeSpan.FromSeconds(90));
}

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(c =>
    {
        c.TimestampFormat = "[HH:mm:ss] ";
        c.SingleLine = true;
    }))
    .AddHostedService<Starter>()
    .AddManagedCrdts()
        .WithGrpcTransport()
        .WithMessagePackSerialization()
        .WithAddressListDiscovery(new[]
        {
            new Uri("http://nyriscrdt_node-0_1:8080"), 
            new Uri("http://nyriscrdt_node-1_1:8080"), 
            new Uri("http://nyriscrdt_node-2_1:8080")
        }
        .Where(uri => !uri.AbsoluteUri.Contains(Environment.GetEnvironmentVariable("NODE_NAME") ?? "&&illegal&&"))
        .ToList());

var app = builder.Build();

app.MapGrpcServices();
app.Run();
