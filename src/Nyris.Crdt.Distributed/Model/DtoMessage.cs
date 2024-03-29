using Nyris.Contracts.Exceptions;
using ProtoBuf;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record DtoMessage<TDto>(
        [property: ProtoMember(1)] string TypeName,
        [property: ProtoMember(2)] InstanceId InstanceId,
        [property: ProtoMember(3)] TDto Value,
        [property: ProtoMember(4)] string TraceId,
        [property: ProtoMember(5)] uint PropagationCounter = 0
    ) : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        [ProtoIgnore]
        private SemaphoreSlim? Semaphore
        {
            get
            {
                if (_semaphore == null && PropagationCounter > 0)
                {
                    _semaphore = new SemaphoreSlim(0, 1);
                }

                return _semaphore;
            }
        }


        public void Complete() => Semaphore?.Release();

        public async Task MaybeWaitForCompletionAsync(CancellationToken cancellationToken = default)
        {
            if (!await ((Semaphore?.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken)
                         ?? Task.FromResult(true))))
            {
                throw new NyrisException($"TraceId: {TraceId}, Could not complete");
            }
        }

        public void Dispose() => _semaphore?.Dispose();
    }
}
