using System;

namespace Nyris.Crdt.Distributed.Metrics;

public interface ICrdtMetricsRegistry
{
    void CollectDtoSize(int? itemSize, int? tombstoneSize, string crdtName);
    void CollectDtoSize(int? itemSize, string crdtName);
    void CollectCollectionSize(int? itemSize, string crdtName);
    void CollectCollectionSize(int? itemSize, int? tombstoneSize, string crdtName);
    void RecordMergeTrigger(string crdtName);
}
