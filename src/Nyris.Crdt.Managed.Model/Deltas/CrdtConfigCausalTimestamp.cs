using System.Collections.Immutable;

namespace Nyris.Crdt.Managed.Model.Deltas;

public sealed record CrdtConfigCausalTimestamp(ImmutableDictionary<ConfigFields, DateTime> DateTimes);