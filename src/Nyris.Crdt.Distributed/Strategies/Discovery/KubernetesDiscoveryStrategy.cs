using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Strategies.Discovery
{
    internal sealed class KubernetesDiscoveryStrategy : IDiscoveryStrategy, IDisposable
    {
        private readonly Kubernetes? _client;
        private readonly KubernetesDiscoveryPodSelectionOptions _options;
        private readonly ILogger<KubernetesDiscoveryStrategy> _logger;
        private readonly bool _isInCluster;

        public KubernetesDiscoveryStrategy(KubernetesDiscoveryPodSelectionOptions options,
            ILogger<KubernetesDiscoveryStrategy> logger)
        {
            _logger = logger;
            _options = options;
            _isInCluster = KubernetesClientConfiguration.IsInCluster();
            if (!_isInCluster) return;

            var config = KubernetesClientConfiguration.InClusterConfig();
            _client = new Kubernetes(config);
        }

        /// <param name="cancellationToken"></param>
        /// <inheritdoc />
        public async IAsyncEnumerable<NodeCandidate> GetNodeCandidates([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_isInCluster) yield break;

            var client = _client;
            Debug.Assert(client != null, "_client != null");

            var pods = new List<V1Pod>();
            foreach (var @namespace in _options.Namespaces)
            {
                var podList = await client.ListNamespacedPodAsync(@namespace, cancellationToken: cancellationToken);
                _logger.LogDebug("Info about {PodNumber} pods retrieved from {Namespace} namespace: {PodNames}",
                    podList.Items.Count, @namespace, string.Join("; ", podList.Items.Select(pod => ModelExtensions.Name(pod))));
                pods.AddRange(podList.Items);
            }

            foreach (var pod in pods.Where(p => _options.KeepPodCondition == null || _options.KeepPodCondition(p)))
            {
                var podIp = pod.Status.PodIP;

                if (!Uri.TryCreate($"http://{podIp}", UriKind.Absolute, out var baseAddress))
                {
                    _logger.LogWarning("Couldn't parse URI for pod {PodName}, IP: {PodIP} - skipping it",
                        pod.Name(), podIp);
                    continue;
                }

                yield return new NodeCandidate(baseAddress, pod.Name());
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
