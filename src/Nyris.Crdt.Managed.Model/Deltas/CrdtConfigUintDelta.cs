namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtConfigUintDelta(ConfigFields Field, uint Value, DateTime DateTime) : CrdtConfigDelta(DateTime);