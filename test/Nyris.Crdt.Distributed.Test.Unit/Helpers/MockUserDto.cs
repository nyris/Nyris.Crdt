using System;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers;

public class MockUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
