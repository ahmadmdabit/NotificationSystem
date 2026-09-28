using Shared.Domain;
using Shared.Domain.Abstractions;

namespace TestDoubles.Mocks;

public static class MockDomainEventDispatcher
{
    public static (IDomainEventDispatcherMock Dispatcher, Arg<DomainEvent> PublishedEvents) Create()
    {
        // 1. Create a matcher variable to automatically capture arguments
        var eventArg = Any<DomainEvent>();
        var mock = IDomainEventDispatcher.Mock();

        // 2. Returns() for Task accepts Func<Task> (0 arguments)
        mock.PublishAsync(eventArg, Any<CancellationToken>())
            .Returns(() => Task.CompletedTask);

        // 3. eventArg.Values exposes the captured events
        return (mock, eventArg);
    }
}
