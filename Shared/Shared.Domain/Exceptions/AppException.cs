namespace Shared.Domain.Exceptions;

/// <summary>
/// Base type for expected application/domain failures.
/// <para>
/// <see cref="Exception.Message"/> may carry diagnostic detail for logs, while
/// <see cref="PublicMessage"/> is the client-safe text (never echoes caller input).
/// </para>
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(AppErrorKind kind, string publicMessage, string? diagnosticMessage = null)
        : base(diagnosticMessage ?? publicMessage)
    {
        Kind = kind;
        PublicMessage = publicMessage;
    }

    protected AppException(AppErrorKind kind, string publicMessage, Exception innerException)
        : base(publicMessage, innerException)
    {
        Kind = kind;
        PublicMessage = publicMessage;
    }

    public AppErrorKind Kind { get; }

    /// <summary>Client-safe message. Must not contain secrets or echo caller-supplied input.</summary>
    public string PublicMessage { get; }
}
