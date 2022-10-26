namespace Nyris.Crdt.Benchmarks;

public static class RandomExtensions
{
    public static void Shuffle<T> (this Random rng, IList<T> array)
    {
        var n = array.Count;
        while (n > 1) 
        {
            var k = rng.Next(n--);
            (array[n], array[k]) = (array[k], array[n]);
        }
    }
}