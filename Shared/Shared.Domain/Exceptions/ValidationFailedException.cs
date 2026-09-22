namespace Shared.Domain.Exceptions;

/// <summary>
/// Raised by the MediatR validation pipeline behavior when FluentValidation reports failures.
/// Replaces FluentValidation's own exception type so the Domain/Application layers do not
/// depend on the validation library and the API layer can map it uniformly.
/// </summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(IReadOnlyList<string> errors)
        : base(AppErrorKind.Validation, "One or more validation errors occurred.")
    {
        Errors = errors ?? [];
    }

    /// <summary>Flattened "Property: message" entries suitable for an API error payload.</summary>
    public IReadOnlyList<string> Errors { get; }
}
