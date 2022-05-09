using System;
using System.Collections.Generic;
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

        public KubernetesDiscoveryStrategy(
            KubernetesDiscoveryPodSelectionOptions options,
            ILogger<KubernetesDiscoveryStrategy> logger
        )
        {
            _logger = logger;
            _options = options;
            _isInCluster = KubernetesClientConfiguration.IsInCluster();

            _logger.LogDebug("Initializing {DiscoveryStrategyName}. Namespaces: {ListOfNamespaces}",
                             nameof(KubernetesDiscoveryStrategy), string.Join(", ", options.Namespaces));

            if (!_isInCluster) return;

            var config = KubernetesClientConfiguration.InClusterConfig();
            _client = new Kubernetes(config);
        }

        /// <param name="cancellationToken"></param>
        /// <inheritdoc />
        public async IAsyncEnumerable<NodeCandidate> GetNodeCandidates(
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            _logger.LogDebug("Running {DiscoveryStrategyName}", nameof(KubernetesDiscoveryStrategy));
            if (!_isInCluster)
            {
                _logger.LogDebug("Kubernetes client did not detect a cluster, aborting");
                yield break;
            }

            var pods = new List<V1Pod>();
            foreach (var @namespace in _options.Namespaces)
            {
                var podList = await _client.ListNamespacedPodAsync(@namespace, cancellationToken: cancellationToken);
                _logger.LogDebug("Info about {PodNumber} pods retrieved from {Namespace} namespace: {PodNames}",
                                 podList.Items.Count, @namespace, string.Join("; ", podList.Items.Select(pod => pod.Name())));
                pods.AddRange(podList.Items);
            }

            _logger.LogDebug("Found {NumberOfPods} pods", pods.Count);
            foreach (var pod in pods)
            {
                var podName = pod.Name();
                if (_options.KeepPodCondition != null && !_options.KeepPodCondition(pod))
                {
                    _logger.LogDebug("Skipping pod {PodName} because if did not satisfy KeepPodCondition", podName);
                    continue;
                }

                var podIp = pod.Status.PodIP;

                if (!Uri.TryCreate($"http://{podIp}:{_options.Port}", UriKind.Absolute, out var baseAddress))
                {
                    _logger.LogWarning("Couldn't parse URI for pod {PodName}, IP: {PodIP} - skipping it",
                                       podName, podIp);
                    continue;
                }

                _logger.LogDebug("Yielding pod {PodName} as node candidate", podName);
                yield return new NodeCandidate(baseAddress, podName);
            }
        }

        public void Dispose() => _client?.Dispose();
    }
}
