using MediatR;
using UserService.Application.DTOs;
using Shared.Domain.Exceptions;
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

    public RegisterUserCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<UserDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Check for existing user
        var existing = await _repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            throw new DuplicateEntityException("User", "Registration failed. The username may already be taken.");

        // Create domain entity
        var user = User.Create(request.Username, request.Password, _passwordHasher);

        UserDto result;
        try
        {
            // Persist first: the identity is assigned by sp_RegisterUser, so the event
            // must be raised from the persisted aggregate (its Id), not the transient one.
            var created = await _repository.InsertAsync(user, cancellationToken).ConfigureAwait(false);
            created.AddDomainEvent(new UserRegisteredEvent(created.Id, created.Username));

            // Dispatch-after-commit: record on the ambient collector; TransactionBehavior
            // drains + publishes it only after the transaction commits (no direct publish here).
            Shared.Domain.DomainEventCollector.AddRange(created.DomainEvents);
            created.ClearDomainEvents();

            result = new UserDto
            {
                Id = created.Id,
                Username = created.Username,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // Check-then-act race closed by UXUsersUsername: map to the same generic 400
            // as the pre-check above (never echo the username — user enumeration).
            throw new DuplicateEntityException("User", "Registration failed. The username may already be taken.", ex);
        }

        return result;
    }

    /// <summary>
    /// Detects SQL Server unique-violation (2601/2627) without referencing a SqlClient
    /// package from the Application layer (Clean Architecture boundary). The Infrastructure
    /// layer throws Microsoft.Data.SqlClient.SqlException; matched by type name + Number.
    /// </summary>
    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var type = ex.GetType();
        if (type.FullName is not ("Microsoft.Data.SqlClient.SqlException" or "System.Data.SqlClient.SqlException"))
            return false;

        var number = type.GetProperty("Number")?.GetValue(ex) as int?;
        return number is 2601 or 2627;
    }
}
