using MediatR;

using UserService.Domain.Abstractions;

namespace UserService.Application.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
{
    private readonly IUserRepository repository;

    public DeleteUserCommandHandler(IUserRepository repository)
    {
        this.repository = repository;
    }

    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        return await repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }
}
