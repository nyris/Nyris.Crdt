using Nyris.Crdt.Transport.Grpc;
using Nyris.ManagedCrdtsV2;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddLogging(loggingBuilder => loggingBuilder.AddConsole())
    .AddGrpcTransport()
    .AddManagedCrdts()
    .WithAddressListDiscovery(new[] { new Uri("http://node-1"), new Uri("http://node-2"), new Uri("http://node-3") });

var app = builder.Build();

app.MapGrpcServices();
app.Run();