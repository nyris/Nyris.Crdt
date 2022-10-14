namespace Nyris.Crdt.Managed.Metadata;

public sealed record CrdtInfoStorageSizeDelta(ulong Value, DateTime DateTime) : CrdtInfoDelta;