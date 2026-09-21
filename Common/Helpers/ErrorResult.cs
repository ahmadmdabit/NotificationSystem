using Microsoft.AspNetCore.Hosting;

namespace Common.Helpers;

public class ErrorResult
{
    public int Code { get; set; }
    public string Message { get; set; }
    public string? StackTrace { get; set; }
    public string? InnerMessage { get; set; }
    public string? InnerStackTrace { get; set; }

    public ErrorResult(int code, string message)
    {
        this.Code = code;
        this.Message = message;
    }

    public ErrorResult(int code, Exception exception, IWebHostEnvironment? env = null)
    {
        this.Code = code;
        this.Message = exception.Message;

        // Only include stack trace details in non-production environments
        if (env != null && !string.Equals(env.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            this.StackTrace = exception.StackTrace;
            this.InnerMessage = exception.InnerException?.Message;
            this.InnerStackTrace = exception.InnerException?.StackTrace;
        }
    }
}