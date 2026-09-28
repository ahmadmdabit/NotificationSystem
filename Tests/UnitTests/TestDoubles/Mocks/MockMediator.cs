using MediatR;

namespace TestDoubles.Mocks;

public static class MockMediator
{
    /// <summary>
    /// Creates a bare <see cref="IMediator"/> mock.
    /// </summary>
    /// <remarks>
    /// There is deliberately no generic "create with a canned response" helper. <c>Send</c> is
    /// declared as <c>Send&lt;TResponse&gt;(IRequest&lt;TResponse&gt;, CancellationToken)</c>, so a
    /// matcher typed <c>Any&lt;TRequest&gt;()</c> only matches when <c>TRequest</c> is exactly
    /// <c>IRequest&lt;TResponse&gt;</c> — which never holds for a concrete command or query. Callers
    /// set up <c>Send</c> with the concrete request type instead:
    /// <c>mock.Send(Arg.Is&lt;IRequest&lt;UserReadDto?&gt;&gt;(q =&gt; q is GetUserByIdQuery { Id: 1 }), ...)</c>.
    /// </remarks>
    public static IMediatorMock Create()
    {
        return IMediator.Mock();
    }
}
