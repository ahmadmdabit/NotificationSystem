using MediatR;
using UserService.Application.DTOs;
using UserService.Domain.Abstractions;

namespace UserService.Application.Queries.GetAllUsers;

/// <summary>
/// Handles GetAllUsersQuery.
/// </summary>
public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserReadDto>>
{
    private readonly IUserRepository _repository;

    public GetAllUsersQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<UserReadDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return users.Select(u => new UserReadDto
        {
            Id = u.Id,
            Username = u.Username,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();
    }
}
