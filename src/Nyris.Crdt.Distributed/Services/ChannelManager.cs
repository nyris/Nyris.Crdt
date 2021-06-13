using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Nyris.Crdt.Distributed.Model;
using ProtoBuf.Grpc.Client;

namespace Nyris.Crdt.Distributed.Services
{
    internal sealed class ChannelManager<TGrpcService> where TGrpcService : class
    {
        private readonly ConcurrentDictionary<NodeId, GrpcChannel> _channels = new();
        private readonly ManagedCrdtContext _context;
        private readonly ConcurrentDictionary<NodeId, TGrpcService> _grpcClients = new();

        public ChannelManager(ManagedCrdtContext context)
        {
            _context = context;
        }

        public bool TryGetProxy<TDto>(NodeId nodeId, [NotNullWhen(true)] out IProxy<TDto>? proxy)
        {
            if (!_grpcClients.TryGetValue(nodeId, out var client)
                && TryGetOrCreate(nodeId, out var channel))
            {
                var interceptor = new FailureInterceptor(_channels, _context, _grpcClients, nodeId);
                client = channel.Intercept(interceptor).CreateGrpcService<TGrpcService>();
                _grpcClients.TryAdd(nodeId, client);
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            // TGrpcService is provided from a user's assembly by a Source Generator and should inherit IProxy
            proxy = client as IProxy<TDto>;
            return proxy != null;
        }

        private bool TryGetOrCreate(NodeId nodeId, [NotNullWhen(true)] out GrpcChannel? channel)
        {
            // Most common shortcut
            if (_channels.TryGetValue(nodeId, out channel)) return true;

            var nodeInfo = _context.Nodes.Value.FirstOrDefault(info => info.Id == nodeId);
            if (nodeInfo == default)
            {
                channel = null;
                return false;
            }

            channel = _channels.GetOrAdd(nodeId, _ => GrpcChannel.ForAddress(nodeInfo.Address));
            return true;
        }

        private sealed class FailureInterceptor : Interceptor
        {
            private readonly ConcurrentDictionary<NodeId, GrpcChannel> _channels;
            private readonly ManagedCrdtContext _context;
            private readonly ConcurrentDictionary<NodeId, TGrpcService> _grpcClients;
            private readonly NodeId _nodeId;

            /// <inheritdoc />
            public FailureInterceptor(ConcurrentDictionary<NodeId, GrpcChannel> channels,
                ManagedCrdtContext context,
                ConcurrentDictionary<NodeId, TGrpcService> grpcClients, NodeId nodeId)

            {
                _channels = channels;
                _context = context;
                _grpcClients = grpcClients;
                _nodeId = nodeId;
            }

            /// <inheritdoc />
            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
                ClientInterceptorContext<TRequest, TResponse> context,
                AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
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
                    await RemoveNode();
                    throw;
                }
            }

            private Task RemoveNode()
            {
                _context.Nodes.RemoveAsync(i => i.Id == _nodeId);
                _grpcClients.TryRemove(_nodeId, out _);
                return _channels.TryRemove(_nodeId, out var channel) ? channel.ShutdownAsync() : Task.CompletedTask;
            }
        }
    }

}