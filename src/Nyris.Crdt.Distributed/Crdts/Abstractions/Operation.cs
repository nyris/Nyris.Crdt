namespace Nyris.Crdt.Distributed.Crdts.Abstractions
{
    public abstract record Operation;

    public abstract record Operation<TResponse> where TResponse : Response;

    public abstract record Response;
}