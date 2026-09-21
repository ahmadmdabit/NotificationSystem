using MediatR;
using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure;

/// <summary>
/// Cross-cutting MediatR pipeline behavior template for logging.
/// </summary>
public sealed class CommonBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<CommonBehavior<TRequest, TResponse>> _logger;

    public CommonBehavior(ILogger<CommonBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("[CommonBehavior] {Request} started", requestName);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var response = await next().ConfigureAwait(false);

        stopwatch.Stop();
        _logger.LogInformation("[CommonBehavior] {Request} completed in {ElapsedMs}ms", requestName, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
