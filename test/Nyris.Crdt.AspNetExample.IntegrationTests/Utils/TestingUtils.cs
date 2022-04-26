using System;
using System.Threading.Tasks;

namespace Nyris.Crdt.GrpcServiceSample.IntegrationTests.Utils;

internal static class TestingUtils
{
    public static async Task WaitFor(Func<Task> func)
    {
        var maxWait = TimeSpan.FromSeconds(60);
        var start = DateTime.Now;
        Exception? exception = null;

        while (start.Add(maxWait) > DateTime.Now)
        {
            try
            {
                await func();

                return;
            }
            catch (Exception e)
            {
                exception = e;
            }

            await Task.Delay(maxWait / 5);
        }

        if (exception is not null)
        {
            throw exception;
        }
    }
}