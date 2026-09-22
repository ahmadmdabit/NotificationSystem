namespace Shared.Domain.Exceptions;

/// <summary>
/// Raised when an operation targets an entity that does not exist (or is soft-deleted).
/// Translates to HTTP 404 at the API boundary.
/// </summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string entityName, object key)
        : base(AppErrorKind.NotFound, $"{entityName} {key} not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }
}
