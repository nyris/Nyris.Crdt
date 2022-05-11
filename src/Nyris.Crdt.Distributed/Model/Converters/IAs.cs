namespace Nyris.Crdt.Distributed.Model.Converters;

public interface IAs<out T>
{
    public T Value { get; }
}
