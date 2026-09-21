using MediatR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for logging command/query execution.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("[LoggingBehavior] {Request} started", requestName);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await next().ConfigureAwait(false);

        stopwatch.Stop();
        _logger.LogInformation("[LoggingBehavior] {Request} completed in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
