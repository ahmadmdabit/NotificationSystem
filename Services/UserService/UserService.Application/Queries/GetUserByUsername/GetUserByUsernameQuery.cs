using MediatR;

using UserService.Application.DTOs;

namespace UserService.Application.Queries.GetUserByUsername;

public sealed class GetUserByUsernameQuery : IRequest<UserReadDto?>
{
    public string Username { get; }

    public GetUserByUsernameQuery(string username)
    {
        Username = username;
    }
}
