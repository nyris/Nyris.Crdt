namespace Nyris.Crdt.Managed.Metadata;

public sealed record CrdtConfigUintDelta(ConfigFields Field, uint Value, DateTime DateTime) : CrdtConfigDelta(DateTime);