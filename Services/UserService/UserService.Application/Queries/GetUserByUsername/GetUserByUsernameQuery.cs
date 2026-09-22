using MediatR;
using UserService.Application.DTOs;
using UserService.Domain.Abstractions;

namespace UserService.Application.Queries.GetUserByUsername;

public sealed class GetUserByUsernameQuery : IRequest<UserReadDto?>
{
    public string Username { get; }

    public GetUserByUsernameQuery(string username)
    {
        Username = username;
    }
}
