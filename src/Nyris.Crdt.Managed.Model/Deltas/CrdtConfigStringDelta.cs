namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtConfigStringDelta(ConfigFields Field, string Value, DateTime DateTime) : CrdtConfigDelta(DateTime);