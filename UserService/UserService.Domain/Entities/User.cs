using Shared.Domain;
using UserService.Domain.Abstractions;
using UserService.Domain.Events;
using UserService.Domain.ValueObjects;

namespace UserService.Domain.Entities;

/// <summary>
/// Rich User entity with domain behavior.
/// </summary>
public sealed class User
{
    public long Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public Password Password { get; private set; } = null!;
    public DateTime? CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Shared.Domain.DomainEvent> _domainEvents = new();
    public IReadOnlyCollection<Shared.Domain.DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private User() { }

    public static User Create(string username, string password, IPasswordHasher hasher)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        var entity = new User
        {
            Username = username,
            Password = Password.Create(password, hasher),
            CreatedAt = DateTime.UtcNow
        };

        return entity;
    }

    public void VerifyPassword(string password, IPasswordHasher hasher)
    {
        if (!hasher.VerifyPassword(password, Password.Hash, Password.Salt))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }
    }

    public void UpdatePassword(string newPassword, IPasswordHasher hasher)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("New password cannot be empty.", nameof(newPassword));

        Password = Password.Create(newPassword, hasher);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddDomainEvent(Shared.Domain.DomainEvent evt) => _domainEvents.Add(evt);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
