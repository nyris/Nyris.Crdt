using Nyris.Crdt.Distributed.Crdts.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nyris.Crdt.AspNetExample;

public sealed class ImageIdIndex : IIndex<string, ImageGuid, ImageInfo>
{
    public const string IndexName = nameof(ImageIdIndex);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ImageGuid, bool>> _dict = new();

    /// <inheritdoc />
    public string UniqueName => IndexName;

    /// <inheritdoc />
    public async Task AddAsync(ImageGuid key, ImageInfo? item, CancellationToken cancellationToken = default)
    {
        if (item?.ImageId is null) return;
        _dict.AddOrUpdate(item.ImageId,
            _ => new ConcurrentDictionary<ImageGuid, bool> { [key] = true },
            (_, ids) =>
            {
                ids.TryAdd(key, true);
                return ids;
            });
    }

    /// <inheritdoc />
    public async Task RemoveAsync(ImageGuid key, ImageInfo? item, CancellationToken cancellationToken = default)
    {
        if (item?.ImageId is null) return;
        if (_dict.TryGetValue(item.ImageId, out var keys))
        {
            keys.TryRemove(key, out _);
            if (keys.IsEmpty)
            {
                _dict.TryRemove(item.ImageId, out _);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IList<ImageGuid>> FindAsync(string input) =>
        _dict.TryGetValue(input, out var keys)
            ? keys.Keys.ToList()
            : ArraySegment<ImageGuid>.Empty;
}
