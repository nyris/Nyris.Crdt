namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtInfoStorageSizeDelta(ulong Value, DateTime DateTime) : CrdtInfoDelta;