using MediatR;

using Microsoft.Extensions.Logging;

using Shared.Application.Behaviors;

using TestDoubles.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.Mocks.Logging;

namespace Shared.Tests.Application;

public sealed class LoggingBehaviorTests
{
    [Test]
    public async Task Handle_SuccessfulRequest_LogsAndReturnsResponse()
    {
        var logger = Mock.Logger<LoggingBehavior<LoggingTestRequest, LoggingTestResponse>>();
        var behavior = new LoggingBehavior<LoggingTestRequest, LoggingTestResponse>(logger);

        var result = await behavior.Handle(new LoggingTestRequest(), LoggingTestFixtures.SuccessDelegate, CancellationToken.None);

        await Assert.That(result).IsEqualTo(new LoggingTestResponse("result"));

        // Both entries are asserted. Deleting either LogInformation call from the behavior would
        // otherwise leave this test green (F-06).
        logger.VerifyLog()
            .AtLevel(LogLevel.Information)
            .ContainingMessage("started")
            .WasCalled(Times.Once);
        logger.VerifyLog()
            .AtLevel(LogLevel.Information)
            .ContainingMessage("completed")
            .WasCalled(Times.Once);
        logger.VerifyNoLog(LogLevel.Error);
    }

    [Test]
    public async Task Handle_WhenNextThrowsException_LogsStartedAndRethrows()
    {
        var logger = Mock.Logger<LoggingBehavior<LoggingTestRequest, LoggingTestResponse>>();
        var behavior = new LoggingBehavior<LoggingTestRequest, LoggingTestResponse>(logger);

        var capturedEx = new InvalidOperationException("handler failed");
        RequestHandlerDelegate<LoggingTestResponse> next = _ =>
        {
            throw capturedEx;
        };

        // Use an async lambda: the delegate assertion expects Task, not Task<TResponse>.
        var ex = await Assert.That(async () =>
            {
                await behavior.Handle(new LoggingTestRequest(), next, CancellationToken.None);
            })
            .Throws<InvalidOperationException>();
        await Assert.That(ex!.Message).IsEqualTo("handler failed");

        // The behavior has no catch block, so "completed" must NOT be logged on this path — that
        // absence is the meaningful half of the assertion.
        logger.VerifyLog()
            .AtLevel(LogLevel.Information)
            .ContainingMessage("started")
            .WasCalled(Times.Once);
        logger.VerifyLog()
            .AtLevel(LogLevel.Information)
            .ContainingMessage("completed")
            .WasNeverCalled();
    }

    [Test]
    public async Task Handle_LogsTheConcreteRequestTypeName()
    {
        // The message template renders {Request} from the runtime type. A behaviour that logged a
        // hard-coded name, or dropped the parameter, would still satisfy the two tests above.
        var logger = Mock.Logger<LoggingBehavior<LoggingTestRequest, LoggingTestResponse>>();
        var behavior = new LoggingBehavior<LoggingTestRequest, LoggingTestResponse>(logger);

        await behavior.Handle(new LoggingTestRequest(), LoggingTestFixtures.SuccessDelegate, CancellationToken.None);

        logger.VerifyLog()
            .AtLevel(LogLevel.Information)
            .ContainingMessage(nameof(LoggingTestRequest))
            .WasCalled(Times.Exactly(2));
    }
}
