using MediatR;

using UserService.Application.DTOs;
using UserService.Application.Mappings;
using UserService.Domain.Abstractions;

namespace UserService.Application.Queries.GetUserByUsername;

public sealed class GetUserByUsernameQueryHandler : IRequestHandler<GetUserByUsernameQuery, UserReadDto?>
{
    private readonly IUserRepository repository;

    public GetUserByUsernameQueryHandler(IUserRepository repository)
    {
        this.repository = repository;
    }

    public async Task<UserReadDto?> Handle(GetUserByUsernameQuery request, CancellationToken cancellationToken)
    {
        var user = await repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        return user?.ToReadDto();
    }
}
