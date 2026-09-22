namespace Shared.Domain.Exceptions;

/// <summary>
/// Transport-agnostic failure classification for expected (non-bug) failures.
/// The API layer maps these to HTTP status codes; the Domain never knows about HTTP.
/// </summary>
public enum AppErrorKind
{
    /// <summary>Caller supplied invalid input (400).</summary>
    Validation = 0,

    /// <summary>Caller request is well-formed but cannot be honoured, e.g. duplicate (400).</summary>
    BadRequest = 1,

    /// <summary>Requested resource does not exist (404).</summary>
    NotFound = 2,

    /// <summary>Caller is not authenticated/authorized for the operation (401).</summary>
    Unauthorized = 3
}
