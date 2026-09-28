using MediatR;

using Shared.Application.Behaviors;

namespace TestDoubles.Helpers;

public sealed record CommandWithResult : ICommand, IRequest<int>;

public sealed record QueryWithoutResult : IRequest<int>;

public static class TransactionTestFixtures
{
    public static readonly RequestHandlerDelegate<int> Return42 = _ => Task.FromResult(42);
    public static readonly RequestHandlerDelegate<int> Return99 = _ => Task.FromResult(99);
}
