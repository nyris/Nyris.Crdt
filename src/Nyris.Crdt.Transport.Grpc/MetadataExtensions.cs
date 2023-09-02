using Grpc.Core;
using Nyris.Crdt.Managed.Model;

namespace Nyris.Crdt.Transport.Grpc;

internal static class MetadataExtensions
{
    private const string TraceIdHeaderKey = "r";
    private const string TimestampHeaderKey = "t";
    private const string NodeIdHeaderKey = "n";
    private const string InstanceIdHeaderKey = "i";
    private const string ShardIdHeaderKey = "s";
    private static readonly Metadata.Entry DoNotSendDeltasEntry = new("f", "1");

    public static Metadata WithTimestamp(this Metadata headers, in ReadOnlyMemory<byte> value)
    {
        headers.Add(TimestampHeaderKey, Convert.ToBase64String(value.Span));
        return headers;
    }
    public static Metadata WithTraceId(this Metadata headers, string value) => headers.With(TraceIdHeaderKey, value);
    public static Metadata WithOrigin(this Metadata headers, in NodeId nodeId) => headers.With(NodeIdHeaderKey, nodeId.ToString());
    public static Metadata With(this Metadata headers, in InstanceId instanceId) => headers.With(InstanceIdHeaderKey, instanceId.ToString());
    public static Metadata With(this Metadata headers, in ShardId shardId) => headers.With(ShardIdHeaderKey, shardId.ToString());

    public static Metadata DoNotSendDeltas(this Metadata headers)
    {
        headers.Add(DoNotSendDeltasEntry);
        return headers;
    }

    public static ReadOnlyMemory<byte> GetTimestamp(this Metadata headers)
    {
        var value = headers.GetValue(TimestampHeaderKey);
        return value is null ? ReadOnlyMemory<byte>.Empty : Convert.FromBase64String(value);
    }

    public static bool DeltasRequested(this Metadata headers) => !headers.Contains(DoNotSendDeltasEntry);

    public static string GetTraceId(this Metadata headers) => headers.GetString(TraceIdHeaderKey);
    public static NodeId GetOrigin(this Metadata headers) => NodeId.FromString(headers.GetString(NodeIdHeaderKey));
    public static InstanceId GetInstanceId(this Metadata headers) => InstanceId.FromString(headers.GetString(InstanceIdHeaderKey));
    public static ShardId GetShardId(this Metadata headers) => ShardId.FromString(headers.GetString(ShardIdHeaderKey));

    private static string GetString(this Metadata headers, string key)
    {
        var val = headers.GetValue(key);
        return val is null ? "" : Uri.UnescapeDataString(val);
    }

    private static Metadata With(this Metadata headers, string key, string value)
    {
        var valueSafe = Uri.EscapeDataString(value);
        headers.Add(key, valueSafe);
        return headers;
    }
}