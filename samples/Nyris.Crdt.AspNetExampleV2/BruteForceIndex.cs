using System.Diagnostics;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExampleV2;

public sealed class BruteForceIndex : IMapObserver<ImageId, DatedValue<float[]>>
{
    public readonly SortedList<ImageId, float[]> _data = new();
    public readonly ReaderWriterLockSlim _lock = new();

    public ImageId Find(float[] value, out float dotProduct)
    {
        _lock.EnterReadLock();
        try
        {
            if (_data.Count == 0)
            {
                dotProduct = float.MinValue;
                return ImageId.Empty;
            } 
                
            dotProduct = float.MinValue;
            var index = 0;

            var values = _data.Values;
            for (var i = 0; i < values.Count; ++i)
            {
                var currentDotProduct = DotProduct(value, values[i]);
                if (currentDotProduct > dotProduct)
                {
                    dotProduct = currentDotProduct;
                    index = i;
                }
            }

            return _data.Keys[index];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static float DotProduct(float[] l, float[] r)
    {
        Debug.Assert(l.Length == r.Length);
        var product = 0.0F;
        for (var i = 0; i < l.Length; ++i)
        {
            product += l[i] * r[i];
        }

        return product;
    }
    
    public void ElementAdded(ImageId key, DatedValue<float[]> value)
    {
        _lock.EnterWriteLock();
        try
        {
            _data.Add(key, value.Value);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void ElementUpdated(ImageId key, DatedValue<float[]> newValue)
    {
        _lock.EnterWriteLock();
        try
        {
            _data[key] = newValue.Value;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void ElementRemoved(ImageId key)
    {
        _lock.EnterWriteLock();
        try
        {
            _data.Remove(key);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}