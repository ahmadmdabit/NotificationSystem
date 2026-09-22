using Shared.Domain;

namespace UserService.Domain.Events;

/// <summary>
/// Domain event raised when a user is registered.
/// </summary>
public sealed class UserRegisteredEvent : DomainEvent
{
    public long UserId { get; }
    public string Username { get; }

    public UserRegisteredEvent(long userId, string username)
    {
        UserId = userId;
        Username = username;
    }
}
