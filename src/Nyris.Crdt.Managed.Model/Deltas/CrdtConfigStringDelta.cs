namespace Nyris.Crdt.Managed.Metadata;

public sealed record CrdtConfigStringDelta(ConfigFields Field, string Value, DateTime DateTime) : CrdtConfigDelta(DateTime);