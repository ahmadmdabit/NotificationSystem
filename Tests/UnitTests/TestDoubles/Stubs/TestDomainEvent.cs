namespace TestDoubles.Stubs;

public sealed class TestDomainEvent : Shared.Domain.DomainEvent
{
    public string Data { get; }

    public TestDomainEvent(string data = "test") => Data = data;
}

public static class TestDomainEventFactory
{
    public static TestDomainEvent Create(string data = "test") => new(data);
}
