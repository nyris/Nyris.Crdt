using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Nyris.Crdt.Distributed.Model;
using Nyris.ManagedCrdtsV2;

namespace Nyris.Crdt.Transport.Grpc;


internal sealed class NodeGrpcClientPool : INodeClientPool, INodeFailureNotifier
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentBag<INodeFailureObserver> _subscribers = new();
    private static readonly TimeSpan ChannelExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ClientExpiration = TimeSpan.FromMinutes(3);

    private static readonly GrpcChannelOptions _options = new()
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
                        InitialBackoff = TimeSpan.FromSeconds(1),
                        MaxBackoff = TimeSpan.FromSeconds(5),
                        BackoffMultiplier = 1.5,
                        RetryableStatusCodes = { StatusCode.Unavailable }
                    }
                }
            }
        }
    };

    public NodeGrpcClientPool(IMemoryCache cache)
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
            return GrpcChannel.ForAddress(address, _options);
        });
    }

    public void SubscribeToNodeFailures(INodeFailureObserver observer) => _subscribers.Add(observer);

    private sealed class FailureInterceptor : Interceptor
    {
        private readonly NodeId _nodeId;
        private readonly ConcurrentBag<INodeFailureObserver> _subscribers;

        private const int TolerateErrorsTimes = 9;
        private int _failures;

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
                var result = await responseTask;
                ResetFailureCounter();
                return result;
            }
            catch (Exception)
            {
                await MaybeRemoveNodeAsync();
                throw;
            }
        }

        private void ResetFailureCounter() => Interlocked.Exchange(ref _failures, 0);

        private async ValueTask MaybeRemoveNodeAsync()
        {
            var failures = Interlocked.Increment(ref _failures);
            if (failures < TolerateErrorsTimes) return;

            foreach (var subscriber in _subscribers)
            {
                await subscriber.NodeFailureObservedAsync(_nodeId);
            }
        }
    }
}