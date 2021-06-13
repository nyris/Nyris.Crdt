using System.Collections.Generic;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.AspNetExample
{
    internal sealed class IntsRegistry : ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>, List<int>, GrowthSetFactory>
    {
        /// <inheritdoc />
        public IntsRegistry(string id) : base(id)
        {
        }

        private IntsRegistry(RegistryDto registryDto) : base(registryDto)
        {
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<RegistryDto> EnumerateDtoBatchesAsync()
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc />
        public override string TypeName { get; } = nameof(IntsRegistry);

        /// <inheritdoc />
        public override async Task<string> GetHashAsync()
        {
            throw new System.NotImplementedException();
        }

        public static readonly IManagedCRDTFactory<IntsRegistry, ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>, List<int>, GrowthSetFactory>, Dictionary<NodeId, HashSet<int>>, RegistryDto>
            DefaultFactory = new RegistryFactory();

        public sealed class RegistryFactory : IManagedCRDTFactory<IntsRegistry, ManagedCrdtRegistry<NodeId, NodeId, ManagedGrowthSet, ManagedGrowthSet, HashSet<int>, List<int>, GrowthSetFactory>, Dictionary<NodeId, HashSet<int>>, RegistryDto>
        {
            /// <inheritdoc />
            public IntsRegistry Create(RegistryDto registryDto) => new(registryDto);
        }
    }
}