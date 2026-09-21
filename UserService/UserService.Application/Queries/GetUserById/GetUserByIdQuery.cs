using MediatR;
using UserService.Application.DTOs;

namespace UserService.Application.Queries.GetUserById;

/// <summary>
/// Query to get a user by ID.
/// </summary>
public sealed class GetUserByIdQuery : IRequest<UserReadDto?>
{
    public long Id { get; }

    public GetUserByIdQuery(long id)
    {
        Id = id;
    }
}
