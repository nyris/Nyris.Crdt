using Nyris.Crdt.Distributed;
using Nyris.Crdt.Distributed.Crdts;
using Nyris.Crdt.Distributed.Model;
using Nyris.Crdt.Distributed.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nyris.Crdt.Tests;

record NodeMock(NodeId Id, ManagedCrdtContext Context, INodeInfoProvider InfoProvider,
    IAsyncQueueProvider QueueProvider)
{
    public static async Task<IList<NodeMock>> PrepareNodeMocksAsync(int n, int queueCapacity = 1)
    {
        var result = new List<NodeMock>(n);
        for (var i = 0; i < n; ++i)
        {
            var id = NodeId.GenerateNew();
            var infoProvider = new ManualNodeInfoProvider(id, new NodeInfo(new Uri($"https://{id}.node"), id));
            var queueProvider = new QueueProvider(queueCapacity);
            var nodes = new NodeSet(new InstanceId(id.ToString()), infoProvider.GetMyNodeInfo(),
                queueProvider: queueProvider);
            var context = new TestContext(infoProvider.GetMyNodeInfo(), nodes);
            await context.Nodes.AddAsync(infoProvider.GetMyNodeInfo());
            result.Add(new NodeMock(id, context, infoProvider, queueProvider));
        }

        for (var i = 0; i < n; ++i)
        {
            for (var j = i + 1; j < n; ++j)
            {
                var dtoI = await result[i].Context.Nodes.ToDtoAsync();
                var dtoJ = await result[j].Context.Nodes.ToDtoAsync();
                await result[i].Context.Nodes.MergeAsync(dtoJ);
                await result[j].Context.Nodes.MergeAsync(dtoI);
            }
        }

        return result;
    }
}
