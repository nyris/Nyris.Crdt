using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Gauge;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Metrics;

// TODO: Make granular activations of metrics for Users,
// i.e implement different Interfaces for different metrics then make then available as options
public class CrdtMetricsRegistry : ICrdtMetricsRegistry
{
    private readonly IMetrics? _metrics;

    private readonly GaugeOptions _dtoItemSizeOptions;
    private readonly GaugeOptions _dtoTombstoneSizeOptions;
    private readonly GaugeOptions _collectionItemSizeOptions;
    private readonly GaugeOptions _collectionTombstoneSizeOptions;
    private readonly CounterOptions _mergeTriggersOptions;
    private readonly string _nodeName;

    public CrdtMetricsRegistry(IMetrics? metrics, NodeInfo nodeInfo)
    {
        _metrics = metrics;
        _nodeName = nodeInfo.Id.ToString();

        const string context = "crdt_app";

        _dtoItemSizeOptions = new GaugeOptions
        {
            Name = "dto_item_size",
            Context = context,
            MeasurementUnit = Unit.Items,
        };
        _dtoTombstoneSizeOptions = new GaugeOptions
        {
            Name = "dto_tombstone_size",
            Context = context,
            MeasurementUnit = Unit.Items,
        };

        _collectionItemSizeOptions = new GaugeOptions
        {
            Name = "collection_item_size",
            Context = context,
            MeasurementUnit = Unit.Items,
        };

        _collectionTombstoneSizeOptions = new GaugeOptions
        {
            Name = "collection_tombstone_size",
            Context = context,
            MeasurementUnit = Unit.Items,
        };

        _mergeTriggersOptions = new CounterOptions
        {
            Name = "merge_triggers",
            Context = context,
            MeasurementUnit = Unit.Calls
        };
    }

    public void CollectDtoSize(int? itemSize, string crdtName)
    {
        var metricTags = new MetricTags(new[] { nameof(crdtName), nameof(_nodeName) }, new[] { crdtName, _nodeName });

        if (itemSize is { } i)
        {
            _metrics?.Measure.Gauge.SetValue(_dtoItemSizeOptions, metricTags, i);
        }
    }

    public void CollectDtoSize(int? itemSize, int? tombstoneSize, string crdtName)
    {
        var metricTags = new MetricTags(new[] { nameof(crdtName), nameof(_nodeName) }, new[] { crdtName, _nodeName });

        if (itemSize is { } i)
        {
            _metrics?.Measure.Gauge.SetValue(_dtoItemSizeOptions, metricTags, i);
        }

        if (tombstoneSize is { } iTombstoneSize)
        {
            _metrics?.Measure.Gauge.SetValue(_dtoTombstoneSizeOptions, metricTags, iTombstoneSize);
        }
    }

    public void CollectCollectionSize(int? itemSize, string crdtName)
    {
        var metricTags = new MetricTags(new[] { nameof(crdtName), nameof(_nodeName) }, new[] { crdtName, _nodeName });

        if (itemSize is { } i)
        {
            _metrics?.Measure.Gauge.SetValue(_collectionItemSizeOptions, metricTags, i);
        }
    }

    public void CollectCollectionSize(int? itemSize, int? tombstoneSize, string crdtName)
    {
        var metricTags = new MetricTags(new[] { nameof(crdtName), nameof(_nodeName) }, new[] { crdtName, _nodeName });

        if (itemSize is { } i)
        {
            _metrics?.Measure.Gauge.SetValue(_collectionItemSizeOptions, metricTags, i);
        }

        if (tombstoneSize is { } iTombstoneSize)
        {
            _metrics?.Measure.Gauge.SetValue(_collectionTombstoneSizeOptions, metricTags, iTombstoneSize);
        }
    }

    public void RecordMergeTrigger(string crdtName)
    {
        var metricTags = new MetricTags(new[] { nameof(crdtName), nameof(_nodeName) }, new[] { crdtName, _nodeName });

        _metrics?.Measure.Counter.Increment(_mergeTriggersOptions, metricTags);
    }
}
