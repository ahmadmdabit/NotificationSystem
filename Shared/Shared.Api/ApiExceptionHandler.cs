using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shared.Domain.Exceptions;
using Shared.Helpers;

namespace Shared.Api;

/// <summary>
/// Single exception-to-HTTP translation point for every service, so the
/// <see cref="ApiResult{T}"/> envelope holds for all failures — not just for the ones
/// a controller happens to catch. Registered via
/// <c>services.AddExceptionHandler&lt;ApiExceptionHandler&gt;()</c> + <c>app.UseExceptionHandler()</c>.
/// </summary>
/// <remarks>
/// The constructor must take <see cref="IHostEnvironment"/>, never a bare <c>string</c>. This type
/// is resolved from the container, and nothing registers a <c>string</c> — a primitive parameter
/// compiles cleanly and then throws the first time an unhandled exception is handled, which is
/// exactly where the <c>ApiResult</c> envelope must be produced. Registering <c>AddSingleton
/// &lt;string&gt;</c> to compensate is not an acceptable fix: a bare <c>string</c> in the container
/// is ambiguous as soon as a second consumer needs one, and leaves a security-relevant value
/// (<c>EnvironmentName</c>, which gates diagnostic redaction) resolvable as an untyped primitive.
/// Pinned by <c>WiringTests.DependencyInjectionTests.*_ApiRegistrations_AllResolve</c>.
/// </remarks>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private const string UnexpectedMessage = "An unexpected error occurred.";
    private const string ValidationMessage = "One or more validation errors occurred.";

    private readonly ILogger<ApiExceptionHandler> logger;
    private readonly IHostEnvironment environment;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, message) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path} -> {StatusCode}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Handled {ExceptionType} for {Method} {Path} -> {StatusCode}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                statusCode);
        }

        // Client-safe message; diagnostic detail (stack trace) is attached only outside Production.
        var error = new ErrorResult(statusCode, message, exception, environment.EnvironmentName);
        if (exception is ValidationFailedException validation)
        {
            error.Details = validation.Errors;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response
            .WriteAsJsonAsync(new ApiResult<object>(false, null!, error), cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private static (int StatusCode, string Message) Map(Exception exception) => exception switch
    {
        ValidationFailedException => (StatusCodes.Status400BadRequest, ValidationMessage),

        // 400 (not 409) is deliberate: the UI BFF treats 400 + success=false on registration
        // as the idempotent "already exists" answer when bootstrapping its service account.
        DuplicateEntityException duplicate => (StatusCodes.Status400BadRequest, duplicate.PublicMessage),

        NotFoundException notFound => (StatusCodes.Status404NotFound, notFound.PublicMessage),

        AppException app => (MapKind(app.Kind), app.PublicMessage),

        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized."),

        _ => (StatusCodes.Status500InternalServerError, UnexpectedMessage)
    };

    private static int MapKind(AppErrorKind kind) => kind switch
    {
        AppErrorKind.NotFound => StatusCodes.Status404NotFound,
        AppErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status400BadRequest
    };
}
