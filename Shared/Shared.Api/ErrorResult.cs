using Microsoft.Extensions.Hosting;

namespace Shared.Helpers;

public class ErrorResult
{
    public int Code { get; set; }
    public string Message { get; set; }
    public string? StackTrace { get; set; }
    public string? InnerMessage { get; set; }
    public string? InnerStackTrace { get; set; }

    /// <summary>Optional per-item detail, e.g. individual validation failures.</summary>
    public IReadOnlyList<string>? Details { get; set; }

    public ErrorResult(int code, string message)
    {
        this.Code = code;
        this.Message = message;
    }

    /// <summary>Uses the exception message as the client-facing message (non-production only for traces).</summary>
    public ErrorResult(int code, Exception exception, IHostEnvironment? env = null)
        : this(code, exception?.Message ?? string.Empty, exception, env)
    {
    }

    /// <summary>
    /// Uses an explicit client-safe message while still attaching diagnostic detail
    /// (stack trace / inner message) in non-production environments only.
    /// </summary>
    public ErrorResult(int code, string message, Exception? exception, IHostEnvironment? env = null)
    {
        this.Code = code;
        this.Message = message;

        // Only include stack trace details in non-production environments
        if (exception is not null && env != null && !string.Equals(env.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            this.StackTrace = exception.StackTrace;
            this.InnerMessage = exception.InnerException?.Message;
            this.InnerStackTrace = exception.InnerException?.StackTrace;
        }
    }
}
