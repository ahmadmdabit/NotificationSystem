using MediatR;
using UserService.Application.DTOs;
using UserService.Application.Mappings;
using UserService.Domain.Abstractions;

namespace UserService.Application.Queries.GetUserById;

/// <summary>
/// Handles GetUserByIdQuery.
/// </summary>
public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserReadDto?>
{
    private readonly IUserRepository _repository;

    public GetUserByIdQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserReadDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return user?.ToReadDto();
    }
}
