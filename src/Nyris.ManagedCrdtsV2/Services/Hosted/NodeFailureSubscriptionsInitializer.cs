using Microsoft.Extensions.Hosting;
using Nyris.Crdt.Transport.Abstractions;

namespace Nyris.ManagedCrdtsV2.Services.Hosted;

internal sealed class NodeFailureSubscriptionsInitializer : IHostedService
{
    public NodeFailureSubscriptionsInitializer(INodeFailureNotifier notifier, IEnumerable<INodeFailureObserver> observers)
    {
        foreach (var observer in observers)
        {
            notifier.SubscribeToNodeFailures(observer);
        }
    }
    
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}