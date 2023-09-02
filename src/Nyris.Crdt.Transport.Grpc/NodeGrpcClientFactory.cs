using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Nyris.Crdt.Managed.Model;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.Crdt.Transport.Grpc;


internal sealed class NodeGrpcClientFactory : INodeClientFactory, INodeFailureNotifier
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentBag<INodeFailureObserver> _subscribers = new();
    private static readonly TimeSpan ChannelExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ClientExpiration = TimeSpan.FromMinutes(5);

    private static readonly GrpcChannelOptions Options = new()
    {
        ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 5,
                        InitialBackoff = TimeSpan.FromMilliseconds(500),
                        MaxBackoff = TimeSpan.FromSeconds(3),
                        BackoffMultiplier = 1.5,
                        RetryableStatusCodes = { StatusCode.Unavailable }
                    }
                }
            }
        }
    };

    public NodeGrpcClientFactory(IMemoryCache cache)
    {
        _cache = cache;
    }

    public INodeClient GetClientForNodeCandidate(Uri address)
    {
        var channel = GetOrCreateChannel(address);
        return new NodeGrpcClient(new Node.NodeClient(channel));
    }

    public INodeClient GetClient(NodeInfo nodeInfo)
    {
        // we want to touch the channel even if the client is cached
        var channel = GetOrCreateChannel(nodeInfo.Address);
        return _cache.GetOrCreate(nodeInfo.Id, entry =>
        {
            entry.SetSlidingExpiration(ClientExpiration);
            return new NodeGrpcClient(new Node.NodeClient(channel
                .Intercept(new FailureInterceptor(nodeInfo.Id, _subscribers))));
        });
    }

    private GrpcChannel GetOrCreateChannel(Uri address)
    {
        return _cache.GetOrCreate(address, entry =>
        {
            entry.SetSlidingExpiration(ChannelExpiration);
            entry.RegisterPostEvictionCallback((_, value, _, _) =>
            {
                var channel = (GrpcChannel)value;
                channel.ShutdownAsync().GetAwaiter().GetResult();
                channel.Dispose();
            });
            return GrpcChannel.ForAddress(address, Options);
        });
    }

    public void SubscribeToNodeFailures(INodeFailureObserver observer) => _subscribers.Add(observer);

    private sealed class FailureInterceptor : Interceptor
    {
        private readonly NodeId _nodeId;
        private readonly ConcurrentBag<INodeFailureObserver> _subscribers;

        private const int TolerateErrorsTimes = 0;

        /// <inheritdoc />
        public FailureInterceptor(NodeId nodeId, ConcurrentBag<INodeFailureObserver> subscribers)
        {
            _nodeId = nodeId;
            _subscribers = subscribers;
        }

        /// <inheritdoc />
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation
        )
        {
            var call = continuation(request, context);

            return new AsyncUnaryCall<TResponse>(HandleResponse(call.ResponseAsync),
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }

        private async Task<TResponse> HandleResponse<TResponse>(Task<TResponse> responseTask)
        {
            try
            {
                return await responseTask;
            }
            catch (Exception)
            {
                await RemoveNodeAsync();
                throw;
            }
        }

        private async Task RemoveNodeAsync()
        {
            foreach (var subscriber in _subscribers)
            {
                await subscriber.NodeFailureObservedAsync(_nodeId);
            }
        }
    }
}
