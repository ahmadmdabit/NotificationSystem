using MediatR;

using UserService.Application.DTOs;
using UserService.Application.Mappings;
using UserService.Domain.Abstractions;

namespace UserService.Application.Queries.GetAllUsers;

/// <summary>
/// Handles GetAllUsersQuery.
/// </summary>
public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserReadDto>>
{
    private readonly IUserRepository repository;

    public GetAllUsersQueryHandler(IUserRepository repository)
    {
        this.repository = repository;
    }

    public async Task<IReadOnlyList<UserReadDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return users.Select(u => u.ToReadDto()).ToList();
    }
}
