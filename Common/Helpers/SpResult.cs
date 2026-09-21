namespace Common.Helpers;

/// <summary>
/// Encapsulates the execution result of ad-hoc queries and stored procedures.
/// </summary>
public sealed record SpResult
{
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
    public object? Data { get; init; }
}