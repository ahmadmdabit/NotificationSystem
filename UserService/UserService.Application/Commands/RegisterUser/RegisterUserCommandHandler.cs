using MediatR;
using UserService.Application.DTOs;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;
using UserService.Domain.Events;

namespace UserService.Application.Commands.RegisterUser;

/// <summary>
/// Handles RegisterUserCommand.
/// </summary>
public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, UserDto>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RegisterUserCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IDomainEventDispatcher eventDispatcher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<UserDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Check for existing user
        var existing = await _repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new InvalidOperationException($"User '{request.Username}' already exists.");

        // Create domain entity
        var user = User.Create(request.Username, request.Password, _passwordHasher);
        user.AddDomainEvent(new UserRegisteredEvent(user.Id, user.Username));

        // Persist
        var created = await _repository.InsertAsync(user, cancellationToken).ConfigureAwait(false);

        // Dispatch domain events
        foreach (var evt in user.DomainEvents)
            await _eventDispatcher.PublishAsync(evt, cancellationToken).ConfigureAwait(false);
        user.ClearDomainEvents();

        return new UserDto
        {
            Id = created.Id,
            Username = created.Username,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        };
    }
}
