namespace Shared.Domain.Exceptions;

/// <summary>
/// Raised when persisting an entity violates a uniqueness constraint.
/// Thrown by repositories after translating provider-specific duplicate-key errors, and by
/// handlers when a pre-check detects an existing entity, so both the check-then-act path and
/// the race path produce the same client contract.
/// </summary>
public sealed class DuplicateEntityException : AppException
{
    public DuplicateEntityException(string entityName)
        : base(AppErrorKind.BadRequest, $"{entityName} already exists.")
    {
        EntityName = entityName;
    }

    public DuplicateEntityException(string entityName, string publicMessage)
        : base(AppErrorKind.BadRequest, publicMessage)
    {
        EntityName = entityName;
    }

    public DuplicateEntityException(string entityName, string publicMessage, Exception innerException)
        : base(AppErrorKind.BadRequest, publicMessage, innerException)
    {
        EntityName = entityName;
    }

    public string EntityName { get; }
}
