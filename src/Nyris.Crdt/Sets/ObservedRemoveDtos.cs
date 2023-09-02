using System;
using System.Collections.Immutable;
using Range = Nyris.Crdt.Model.Range;

namespace Nyris.Crdt.Sets
{
    public static class ObservedRemoveDtos<TActorId, TItem>
        where TActorId : IEquatable<TActorId>
        where TItem : notnull
    {
        public sealed record Dto(ImmutableDictionary<TActorId, ImmutableArray<Range>> VersionContext,
                                 ImmutableDictionary<TActorId, ImmutableDictionary<TItem, ulong>> Items);

        public sealed record CausalTimestamp(ImmutableDictionary<TActorId, ImmutableArray<Range>> Since);

        public abstract record DeltaDto(TActorId Actor)
        {
            public static DeltaDto Added(TItem item, TActorId actor, ulong version)
                => new DeltaDtoAddition(item, actor, version);
            public static DeltaDto Removed(TActorId actor, Range range)
                => new DeltaDtoDeletedRange(actor, range);
            public static DeltaDto Removed(TActorId actor, ulong version)
                => new DeltaDtoDeletedDot(actor, version);
        }

        public sealed record DeltaDtoAddition(TItem Item, TActorId Actor, ulong Version) : DeltaDto(Actor);

        public sealed record DeltaDtoDeletedDot(TActorId Actor, ulong Version) : DeltaDto(Actor);

        public sealed record DeltaDtoDeletedRange(TActorId Actor, Range Range) : DeltaDto(Actor);

    }
}
