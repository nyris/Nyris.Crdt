using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;

namespace Nyris.Crdt.Distributed.Model
{
    [ProtoContract(SkipConstructor = true)]
    public sealed record DtoMessage<TDto>([property: ProtoMember(1)] string TypeName,
        [property: ProtoMember(2)] string InstanceId,
        [property: ProtoMember(3)] TDto Value,
        [property: ProtoMember(4)] int PropagationCounter = 0)
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

        public Task MaybeWaitForCompletionAsync(CancellationToken cancellationToken = default)
            => Semaphore?.WaitAsync(cancellationToken) ?? Task.CompletedTask;
    }
}