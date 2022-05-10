using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ConsoleApp;

internal static class Program
{
    private static readonly BufferBlock<int> Block = new(new DataflowBlockOptions { BoundedCapacity = 1 });

    public static async Task Main(string[] args)
    {
        var tasks = Enumerable.Range(0, 200).Select(i => Block.SendAsync(i)).ToList();

        for (var i = 0; i < 10; ++i)
        {
            Console.WriteLine(await Block.ReceiveAsync(CancellationToken.None));
        }
    }
}
