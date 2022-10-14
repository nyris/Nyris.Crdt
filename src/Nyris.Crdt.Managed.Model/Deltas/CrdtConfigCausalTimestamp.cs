using System.Collections.Immutable;

namespace Nyris.Crdt.Managed.Metadata;

public sealed record CrdtConfigCausalTimestamp(ImmutableDictionary<ConfigFields, DateTime> DateTimes);