using Grpc.Core;
using Nyris.Crdt.GrpcServiceSample;

namespace Nyris.Crdt.GrpcServiceSample.Services;

public class GreeterService : Api.ApiBase
{
    private readonly ILogger<GreeterService> _logger;

    public GreeterService(ILogger<GreeterService> logger)
    {
        _logger = logger;
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name
        });
    }
}