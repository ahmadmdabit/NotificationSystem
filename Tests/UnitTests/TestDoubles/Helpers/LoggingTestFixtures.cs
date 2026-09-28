using MediatR;

namespace TestDoubles.Helpers;

public sealed record LoggingTestRequest : IRequest<LoggingTestResponse>;

public sealed record LoggingTestResponse(string Value);

public static class LoggingTestFixtures
{
    public static readonly RequestHandlerDelegate<LoggingTestResponse> SuccessDelegate =
        _ => Task.FromResult(new LoggingTestResponse("result"));
}
