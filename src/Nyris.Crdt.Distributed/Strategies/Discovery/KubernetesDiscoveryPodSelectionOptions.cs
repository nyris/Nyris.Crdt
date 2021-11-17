using System;
using System.Collections.Generic;
using k8s.Models;

namespace Nyris.Crdt.Distributed.Strategies.Discovery
{
    public sealed class KubernetesDiscoveryPodSelectionOptions
    {
        public IEnumerable<string> Namespaces { get; set; } = new[] {"default"};

        public int Port { get; set; } = 8080;

        /// <summary>
        /// If set, filter out pods, for which this function returns false. Keep all if null.
        /// </summary>
        public Func<V1Pod, bool>? KeepPodCondition { get; set; }
    }
}