using MediatR;
using UserService.Application.DTOs;
using UserService.Application.Mappings;
using UserService.Domain.Abstractions;

namespace UserService.Application.Queries.GetUserByUsername;

public sealed class GetUserByUsernameQueryHandler : IRequestHandler<GetUserByUsernameQuery, UserReadDto?>
{
    private readonly IUserRepository _repository;

    public GetUserByUsernameQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserReadDto?> Handle(GetUserByUsernameQuery request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        return user?.ToReadDto();
    }
}
