using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.Sets;

public class ObservedRemoveSetV3<TActorId, TItem> : ObservedRemoveCore<TActorId, TItem>, ISetChangesNotifier<TItem>
    where TItem : IEquatable<TItem>
    where TActorId : IEquatable<TActorId>
{
    // keep everything in array for slightly faster indexing - it is assumed that adding observers
    // is a very rare operation, while notifications happen all the time
    private ISetObserver<TItem>[] _observers = Array.Empty<ISetObserver<TItem>>();
    private readonly object _observersLock = new();

    // I hate using both lock and concurrent dictionary. However, it's very inconvenient to
    // constrain all updates to ConcurrentDictionary methods (hence - lock).
    // But if I were to use normal Dictionary, allowing public enumeration of keys is a pain
    // Proper fully optimized version would look similar to internals of ConcurrentDictionary
    private readonly ConcurrentDictionary<TItem, List<Dot<TActorId>>> _items = new();
    // private readonly object _writeLock = new();

    public int Count => _items.Count;
    public bool Contains(TItem item) => _items.ContainsKey(item);
    public ICollection<TItem> Values => _items.Keys;

    public ImmutableArray<DeltaDto> Add(TItem item, TActorId actorId)
    {
        ulong version;
        lock (WriteLock)
        {
            version = GetNewVersion(actorId);
            AddDot(actorId, version, item);
            AddToContextAndInverse(actorId, version, item);
        }

        return ImmutableArray.Create(DeltaDto.Added(item, actorId, version));
    }

    public ImmutableArray<DeltaDto> Remove(TItem item)
    {
        List<Dot<TActorId>>? dots;

        lock (WriteLock)
        {
            if (!_items.TryRemove(item, out dots))
            {
                return ImmutableArray<DeltaDto>.Empty;
            }
        }

        var builder = ImmutableArray.CreateBuilder<DeltaDto>(dots.Count);
        foreach (var (actor, version) in dots)
        {
            RemoveFromInverse(actor, version);
            builder.Add(DeltaDto.Removed(actor, version));
        }

        var deltas = builder.MoveToImmutable();

        NotifyRemoved(item);
        return deltas;
    }

    public void SubscribeToChanges(ISetObserver<TItem> observer)
    {
        lock(_observersLock)
        {
            var newArray = new ISetObserver<TItem>[_observers.Length + 1];
            Array.Copy(_observers, newArray, _observers.Length);
            newArray[_observers.Length] = observer;
            _observers = newArray;
        }
    }

    protected sealed override ulong? AddDot(TActorId actorId, ulong version, TItem item)
    {
        var dots = _items.GetOrAdd(item, _ => new List<Dot<TActorId>>(1));
        var added = dots.Count == 0;
        var oldVersion = AddOrUpdateDot(dots, actorId, version);

        if(added) NotifyAdded(item);
        return oldVersion;
    }

    protected sealed override void RemoveDot(TActorId actorId, ulong version, TItem item)
    {
        var removed = false;
        lock (WriteLock)
        {
            if (!_items.TryGetValue(item, out var dots)) return;

            var i = GetDotIndex(dots, actorId);
            if (i < 0 || dots[i].Version != version) return;

            dots.RemoveAt(i);

            if (dots.Count == 0)
            {
                removed = _items.TryRemove(item, out _);
            }
        }

        if(removed) NotifyRemoved(item);
    }

    private void NotifyAdded(TItem item)
    {
        foreach (var observer in _observers) observer.ElementAdded(item);
    }

    private void NotifyRemoved(TItem item)
    {
        foreach (var observer in _observers) observer.ElementRemoved(item);
    }

    private static ulong? AddOrUpdateDot(List<Dot<TActorId>> dots, TActorId actorId, ulong version)
    {
        var i = GetDotIndex(dots, actorId);

        if (i < 0)
        {
            dots.Add(new Dot<TActorId>(actorId, version));
            return null;
        }

        var foundVersion = dots[i].Version;
        if (foundVersion >= version) return null;

        dots[i] = new Dot<TActorId>(actorId, version);
        return foundVersion;
    }

    private static int GetDotIndex(List<Dot<TActorId>> dots, TActorId actorId)
    {
        var index = -1;
        for (var i = 0; i < dots.Count; ++i)
        {
            if (!dots[i].Actor.Equals(actorId)) continue;

            index = i;
            break;
        }

        return index;
    }
}