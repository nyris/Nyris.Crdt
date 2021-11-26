namespace Nyris.Crdt.Distributed
{
    internal interface IHashableAndHaveUniqueName : IHashable
    {
        string TypeName { get; }
    }
}