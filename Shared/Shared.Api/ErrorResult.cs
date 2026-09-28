namespace Shared.Helpers;

public class ErrorResult
{
    /// <summary>
    /// The only environments permitted to receive diagnostic detail. Anything not on this list —
    /// including <c>null</c>, an empty string, or a misspelt name — is redacted.
    /// </summary>
    /// <remarks>
    /// This is an allowlist, not a denylist, on purpose. The pre-diff code tested
    /// <c>!string.Equals(environmentName, "Production")</c>, which is fail-OPEN: a null name
    /// satisfies the test and emits the full exception chain. Removing the <c>env != null</c>
    /// conjunct turned the default argument of the <c>(int, Exception, string?)</c> overload into
    /// a disclosure path (F-02). An allowlist makes the unsafe branch unreachable by default.
    /// <para>
    /// Staging is deliberately absent: this deployment runs Development and Production only. If
    /// Staging is introduced it must be added here explicitly, which is the intended friction.
    /// </para>
    /// </remarks>
    private static readonly string[] DiagnosticEnvironments = ["Development", "Local", "Test"];

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
    public ErrorResult(int code, Exception exception, string? environmentName = null)
        : this(code, exception?.Message ?? string.Empty, exception, environmentName)
    {
    }

    /// <summary>
    /// Uses an explicit client-safe message while still attaching diagnostic detail
    /// (stack trace / inner message) in recognised non-production environments only.
    /// </summary>
    public ErrorResult(int code, string message, Exception? exception, string? environmentName = null)
    {
        this.Code = code;
        this.Message = message;

        // Fail closed: an unknown or absent environment name redacts. See DiagnosticEnvironments.
        if (exception is not null
            && environmentName is not null
            && DiagnosticEnvironments.Contains(environmentName, StringComparer.OrdinalIgnoreCase))
        {
            this.StackTrace = exception.StackTrace;
            this.InnerMessage = exception.InnerException?.Message;
            this.InnerStackTrace = exception.InnerException?.StackTrace;
        }
    }
}
