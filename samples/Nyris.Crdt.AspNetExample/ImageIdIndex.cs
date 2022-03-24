using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nyris.Crdt.Distributed.Crdts.Interfaces;

namespace Nyris.Crdt.AspNetExample
{
    public sealed class ImageIdIndex : IIndex<string, ImageGuid, ImageInfo>
    {
        public const string IndexName = nameof(ImageIdIndex);

        private readonly ConcurrentDictionary<string, List<ImageGuid>> _dict = new();
        /// <inheritdoc />
        public string UniqueName => IndexName;

        /// <inheritdoc />
        public async Task AddAsync(ImageGuid key, ImageInfo item, CancellationToken cancellationToken = default)
        {
            _dict.AddOrUpdate(item.ImageId, _ => new List<ImageGuid> { key }, (_, list) =>
            {
                list.Add(key);
                return list;
            });
        }

        /// <inheritdoc />
        public async Task RemoveAsync(ImageGuid key, ImageInfo item, CancellationToken cancellationToken = default)
        {
            if (item?.ImageId is null) return;
            if (_dict.TryGetValue(item.ImageId, out var keys))
            {
                keys.Remove(key);
                if (keys.Count == 0)
                {
                    _dict.TryRemove(item.ImageId, out _);
                }
            }
        }

        /// <inheritdoc />
        public async Task<IList<ImageGuid>> FindAsync(string input) =>
            _dict.TryGetValue(input, out var keys)
                ? keys
                : ArraySegment<ImageGuid>.Empty;
    }
}