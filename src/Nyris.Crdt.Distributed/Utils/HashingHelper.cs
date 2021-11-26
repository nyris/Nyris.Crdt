using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Nyris.Crdt.Distributed.Utils
{
    internal sealed class HashingHelper
    {
        public static ReadOnlySpan<byte> Combine(ReadOnlySpan<byte> obj1, ReadOnlySpan<byte> obj2)
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(obj1);
            sha1.AppendData(obj2);
            return sha1.GetCurrentHash();
        }

        public static byte[] Combine(IHashable obj1, IHashable obj2)
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(obj1.GetHash());
            sha1.AppendData(obj2.GetHash());
            return sha1.GetCurrentHash();
        }

        public static byte[] Combine(IHashable obj1, IHashable obj2, IHashable obj3)
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(obj1.GetHash());
            sha1.AppendData(obj2.GetHash());
            sha1.AppendData(obj3.GetHash());
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine(params IHashable[] items)
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            for (var i = 0; i < items.Length; ++i)
            {
                sha1.AppendData(items[i].GetHash());
            }
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> items)
            where TKey : IHashable
            where TValue : IHashable
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            foreach (var (key, item) in items)
            {
                sha1.AppendData(key.GetHash());
                sha1.AppendData(item.GetHash());
            }
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine<TKey>(IEnumerable<KeyValuePair<TKey, uint>> items)
            where TKey : IHashable
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            foreach (var (key, item) in items)
            {
                sha1.AppendData(key.GetHash());
                sha1.AppendData(BitConverter.GetBytes(item));
            }
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine<TKey, TItem, TActorId>(IEnumerable<KeyValuePair<TKey, VersionedSignedItem<TActorId, TItem>>> items)
            where TKey : IHashable
            where TItem : IEquatable<TItem>, IHashable
            where TActorId : IEquatable<TActorId>, IHashable
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            foreach (var (key, item) in items)
            {
                sha1.AppendData(key.GetHash());
                sha1.AppendData(item.Actor.GetHash());
                sha1.AppendData(item.Item.GetHash());
                sha1.AppendData(BitConverter.GetBytes(item.Version));
            }
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine<TKey, TItem, TTimeStamp>(IEnumerable<KeyValuePair<TKey, TimeStampedItem<TItem,TTimeStamp>>> items)
            where TKey : IEquatable<TKey>
            where TItem : IHashable
            where TTimeStamp : IComparable<TTimeStamp>, IEquatable<TTimeStamp>
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            foreach (var (key, item) in items)
            {
                // TODO: GetHashCode will not be equal across nodes for some types. How to enforce it properly?
                sha1.AppendData(BitConverter.GetBytes(key.GetHashCode()));
                sha1.AppendData(BitConverter.GetBytes(item.Deleted));
                sha1.AppendData(item.Value.GetHash());
                sha1.AppendData(BitConverter.GetBytes(item.TimeStamp.GetHashCode()));
            }
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine<TItem, TActorId>(IEnumerable<VersionedSignedItem<TActorId, TItem>> items)
            where TItem : IEquatable<TItem>, IHashable
            where TActorId : IEquatable<TActorId>, IHashable
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            foreach (var item in items)
            {
                sha1.AppendData(item.Actor.GetHash());
                sha1.AppendData(item.Item.GetHash());
                sha1.AppendData(BitConverter.GetBytes(item.Version));
            }
            return sha1.GetCurrentHash();
        }

        public static byte[] Combine(int value, params IHashable[] items)
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(BitConverter.GetBytes(value));
            for (var i = 0; i < items.Length; ++i)
            {
                sha1.AppendData(items[i].GetHash());
            }
            return sha1.GetCurrentHash();
        }

        public static ReadOnlySpan<byte> Combine(Uri value, params IHashable[] items)
        {
            using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            sha1.AppendData(Encoding.UTF8.GetBytes(value.ToString()));
            for (var i = 0; i < items.Length; ++i)
            {
                sha1.AppendData(items[i].GetHash());
            }
            return sha1.GetCurrentHash();
        }
    }
}