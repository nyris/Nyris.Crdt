using Newtonsoft.Json;

namespace Nyris.Crdt
{
    internal readonly struct VersionedItem<TItem>
    {
        public static VersionedItem<TItem> New(TItem item) => new(item, 1);

        public static VersionedItem<TItem> FromOld(TItem item, VersionedItem<TItem> old) => new(item, old.Version + 1);
        public static VersionedItem<TItem> FromOld(VersionedItem<TItem> old) => new(old.Item, old.Version + 1);

        [JsonConstructor]
        public VersionedItem(TItem item, int version)
        {
            Item = item;
            Version = version;
        }

        public TItem Item { get; }

        public int Version { get; }
    }
}
