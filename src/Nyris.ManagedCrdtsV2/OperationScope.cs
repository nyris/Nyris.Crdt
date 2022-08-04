namespace Nyris.ManagedCrdtsV2;

// Using structs that implement IDisposable is an anti-pattern. However, in this particular case it seems
// that it is not harmful? The goal is to not allocate object on heap every time we need a scope
// 
// using var scope = ... should not result in boxing:
// (see https://stackoverflow.com/questions/2412981/if-my-struct-implements-idisposable-will-it-be-boxed-when-used-in-a-using-statem)
//
// TODO: are there any side effects of making this a struct that invalidates this logic? 
public readonly struct OperationScope : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;

    public OperationScope(ReaderWriterLockSlim @lock)
    {
        _lock = @lock;
    }

    public void Dispose() => _lock.ExitReadLock();
}