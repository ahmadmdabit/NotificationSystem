using MediatR;

using UserService.Application.DTOs;

namespace UserService.Application.Queries.GetAllUsers;

/// <summary>
/// Query to get all users.
/// </summary>
public sealed class GetAllUsersQuery : IRequest<IReadOnlyList<UserReadDto>>
{
}
