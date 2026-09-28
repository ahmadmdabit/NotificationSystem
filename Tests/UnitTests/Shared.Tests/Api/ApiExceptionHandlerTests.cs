using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Shared.Api;
using Shared.Domain.Exceptions;

using TestDoubles.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.Mocks.Logging;

namespace Shared.Tests.Api;

public sealed class ApiExceptionHandlerTests
{
    private static (MockLogger<ApiExceptionHandler> Logger, ApiExceptionHandler Handler) CreateHandler(
        string environmentName = "Development")
    {
        var logger = Mock.Logger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger, new TestHostEnvironment { EnvironmentName = environmentName });
        return (logger, handler);
    }

    [Test]
    public async Task TryHandleAsync_WhenNotFoundException_Returns404WithPublicMessage()
    {
        var (logger, handler) = CreateHandler();
        var context = HttpContextTestFactory.CreateHttpContext();

        var handled = await handler.TryHandleAsync(context, new NotFoundException("User", 42), CancellationToken.None);

        await Assert.That(handled).IsTrue();
        await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);

        // 4xx is a handled condition, not a fault: the level split is what makes log-based
        // alerting on 5xx spikes work, so it is asserted rather than assumed.
        logger.VerifyLog()
            .AtLevel(LogLevel.Warning)
            .WithException<NotFoundException>()
            .WasCalled(Times.Once);
        logger.VerifyNoLog(LogLevel.Error);
    }

    [Test]
    public async Task TryHandleAsync_WhenValidationException_Returns400WithErrors()
    {
        var (logger, handler) = CreateHandler();
        var context = HttpContextTestFactory.CreateHttpContext();
        var errors = new List<string> { "Username is required.", "Email is invalid." };

        var handled = await handler.TryHandleAsync(context, new ValidationFailedException(errors), CancellationToken.None);

        await Assert.That(handled).IsTrue();
        await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);

        logger.VerifyLog()
            .AtLevel(LogLevel.Warning)
            .WithException<ValidationFailedException>()
            .WasCalled(Times.Once);
        logger.VerifyNoLog(LogLevel.Error);
    }

    [Test]
    public async Task TryHandleAsync_WhenUnhandledException_Returns500InternalServerError()
    {
        var (logger, handler) = CreateHandler();
        var context = HttpContextTestFactory.CreateHttpContext();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("unexpected"), CancellationToken.None);

        await Assert.That(handled).IsTrue();
        await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);

        logger.VerifyLog()
            .AtLevel(LogLevel.Error)
            .WithException<InvalidOperationException>()
            .WasCalled(Times.Once);
        logger.VerifyNoLog(LogLevel.Warning);
    }

    /// <summary>
    /// A thrown-and-caught exception with an inner exception. Both halves matter: an exception
    /// that is only <c>new</c>-ed has a null <c>StackTrace</c>, which would make the
    /// "diagnostics are attached" assertion pass for the wrong reason.
    /// </summary>
    private static InvalidOperationException ThrownException()
    {
        try
        {
            throw new InvalidOperationException("boom", new FormatException("inner boom"));
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    [Test]
    public async Task TryHandleAsync_WhenProduction_RedactsDiagnosticDetailFromResponse()
    {
        var (_, handler) = CreateHandler("Production");
        var (context, body) = HttpContextTestFactory.CreateHttpContextWithReadableBody();

        await handler.TryHandleAsync(context, ThrownException(), CancellationToken.None);

        var json = await HttpContextTestFactory.ReadBodyAsync(body);
        await Assert.That(json).IsNotEmpty()
            .Because("An empty body would make every assertion below vacuously true");
        await Assert.That(json).DoesNotContain("boom")
            .Because("A Production response must never carry the exception message (F-02)");
        // The property is always serialised; its VALUE is what carries the leak.
        await Assert.That(json).Contains("\"stackTrace\":null")
            .Because("Production must emit the field as null, never a stack trace");
        await Assert.That(json).Contains("\"innerMessage\":null");
    }

    [Test]
    public async Task TryHandleAsync_WhenNonProduction_IncludesDiagnosticDetailInResponse()
    {
        var (_, handler) = CreateHandler("Development");
        var (context, body) = HttpContextTestFactory.CreateHttpContextWithReadableBody();

        await handler.TryHandleAsync(context, ThrownException(), CancellationToken.None);

        var json = await HttpContextTestFactory.ReadBodyAsync(body);
        await Assert.That(json).IsNotEmpty()
            .Because("An empty body would make every assertion below vacuously true");
        // The envelope's `message` is the client-safe one in EVERY environment; only the
        // diagnostic fields vary. Asserting on the value, not on a bare "boom" substring, which
        // legitimately appears inside innerMessage here.
        await Assert.That(json).Contains("\"message\":\"An unexpected error occurred.\"")
            .Because("The exception message must never become the client-facing message");
        await Assert.That(json).DoesNotContain("\"stackTrace\":null")
            .Because("A recognised non-production environment must still surface stack traces to developers");
        await Assert.That(json).Contains("inner boom")
            .Because("The inner exception message is part of the diagnostic detail developers need");
    }

    [Test]
    public async Task Constructor_WhenEnvironmentIsNull_Throws()
    {
        var logger = Mock.Logger<ApiExceptionHandler>();

        var error = Assert.Throws<ArgumentNullException>(
            () => new ApiExceptionHandler(logger, null!));

        await Assert.That(error.ParamName).IsEqualTo("environment");
    }

    [Test]
    public async Task Constructor_WhenLoggerIsNull_Throws()
    {
        var error = Assert.Throws<ArgumentNullException>(
            () => new ApiExceptionHandler(null!, new TestHostEnvironment()));

        await Assert.That(error.ParamName).IsEqualTo("logger");
    }
}