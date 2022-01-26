using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Nyris.Crdt.Distributed.Utils
{
    internal static class HashingHelper
    {
        private static readonly ObjectPool<IncrementalHash> Pool =
            new DefaultObjectPool<IncrementalHash>(new DoNothingOnReturnHashPoolPolicy());

        public static ReadOnlySpan<byte> Combine(ReadOnlySpan<byte> obj1, ReadOnlySpan<byte> obj2)
        {
            var sha1 = Pool.Get();
            try
            {
                sha1.AppendData(obj1);
                sha1.AppendData(obj2);
                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine(IHashable obj1, IHashable obj2)
        {
            var sha1 = Pool.Get();
            try
            {
                sha1.AppendData(obj1.CalculateHash());
                sha1.AppendData(obj2.CalculateHash());
                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine(IHashable obj1, IHashable obj2, IHashable obj3)
        {
            var sha1 = Pool.Get();
            try
            {
                sha1.AppendData(obj1.CalculateHash());
                sha1.AppendData(obj2.CalculateHash());
                sha1.AppendData(obj3.CalculateHash());
                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine(params IHashable[] items)
        {
            var sha1 = Pool.Get();
            try
            {
                for (var i = 0; i < items.Length; ++i)
                {
                    sha1.AppendData(items[i].CalculateHash());
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> items)
            where TKey : IHashable
            where TValue : IHashable
        {
            var sha1 = Pool.Get();
            try
            {
                foreach (var (key, item) in items)
                {
                    sha1.AppendData(key.CalculateHash());
                    sha1.AppendData(item.CalculateHash());
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine<TKey>(IEnumerable<KeyValuePair<TKey, uint>> items)
            where TKey : IHashable
        {
            var sha1 = Pool.Get();
            try
            {
                foreach (var (key, item) in items)
                {
                    sha1.AppendData(key.CalculateHash());
                    sha1.AppendData(BitConverter.GetBytes(item));
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine<TKey, TItem, TActorId>(
            IEnumerable<KeyValuePair<TKey, VersionedSignedItem<TActorId, TItem>>> items)
            where TKey : IHashable
            where TItem : IEquatable<TItem>, IHashable
            where TActorId : IEquatable<TActorId>, IHashable
        {
            var sha1 = Pool.Get();
            try
            {
                foreach (var (key, item) in items)
                {
                    sha1.AppendData(key.CalculateHash());
                    sha1.AppendData(item.Actor.CalculateHash());
                    sha1.AppendData(item.Item.CalculateHash());
                    sha1.AppendData(BitConverter.GetBytes(item.Version));
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine<TKey, TItem, TTimeStamp>(
            IEnumerable<KeyValuePair<TKey, TimeStampedItem<TItem, TTimeStamp>>> items)
            where TKey : IEquatable<TKey>
            where TItem : IHashable
            where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
        {
            var sha1 = Pool.Get();
            try
            {
                foreach (var (key, item) in items)
                {
                    sha1.AppendData(CalculateHash(key));
                    sha1.AppendData(BitConverter.GetBytes(item.Deleted));
                    sha1.AppendData(item.Value.CalculateHash());
                    sha1.AppendData(CalculateHash(item.TimeStamp));
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine<TItem, TActorId>(
            IEnumerable<VersionedSignedItem<TActorId, TItem>> items)
            where TItem : IEquatable<TItem>, IHashable
            where TActorId : IEquatable<TActorId>, IHashable
        {
            var sha1 = Pool.Get();
            try
            {
                foreach (var item in items)
                {
                    sha1.AppendData(item.Actor.CalculateHash());
                    sha1.AppendData(item.Item.CalculateHash());
                    sha1.AppendData(BitConverter.GetBytes(item.Version));
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static byte[] Combine(int value, params IHashable[] items)
        {
            var sha1 = Pool.Get();
            try
            {
                sha1.AppendData(BitConverter.GetBytes(value));
                for (var i = 0; i < items.Length; ++i)
                {
                    sha1.AppendData(items[i].CalculateHash());
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        public static ReadOnlySpan<byte> Combine(Uri value, params IHashable[] items)
        {
            var sha1 = Pool.Get();
            try
            {
                sha1.AppendData(Encoding.UTF8.GetBytes(value.ToString()));
                for (var i = 0; i < items.Length; ++i)
                {
                    sha1.AppendData(items[i].CalculateHash());
                }

                return sha1.GetHashAndReset();
            }
            finally
            {
                Pool.Return(sha1);
            }
        }

        private static ReadOnlySpan<byte> CalculateHash<T>(T value) where T : notnull
        {
            return value switch
            {
                short shortValue => BitConverter.GetBytes(shortValue),
                int intValue => BitConverter.GetBytes(intValue),
                long longValue => BitConverter.GetBytes(longValue),
                ushort ushortValue => BitConverter.GetBytes(ushortValue),
                uint uintValue => BitConverter.GetBytes(uintValue),
                ulong ulongValue => BitConverter.GetBytes(ulongValue),
                float floatValue => BitConverter.GetBytes(floatValue),
                double doubleValue => BitConverter.GetBytes(doubleValue),
                char charValue => BitConverter.GetBytes(charValue),
                string stringValue => Encoding.Default.GetBytes(stringValue),
                DateTime dateTime => BitConverter.GetBytes(dateTime.ToBinary()),
                IHashable hashable => hashable.CalculateHash(),
                _ => BitConverter.GetBytes(value.GetHashCode())
            };
        }

        private sealed class DoNothingOnReturnHashPoolPolicy : IPooledObjectPolicy<IncrementalHash>
        {
            /// <inheritdoc />
            public IncrementalHash Create() => IncrementalHash.CreateHash(HashAlgorithmName.SHA1);

            /// <inheritdoc />
            public bool Return(IncrementalHash obj) => true;
        }
    }
}