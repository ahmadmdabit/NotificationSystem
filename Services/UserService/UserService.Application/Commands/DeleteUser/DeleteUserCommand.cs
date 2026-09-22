using Shared.Application.Behaviors;

namespace UserService.Application.Commands.DeleteUser;

public sealed class DeleteUserCommand : MediatR.IRequest<bool>, ICommand
{
    public long Id { get; }

    public DeleteUserCommand(long id)
    {
        Id = id;
    }
}
