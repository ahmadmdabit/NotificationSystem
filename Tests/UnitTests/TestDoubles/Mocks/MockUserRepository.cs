using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace TestDoubles.Mocks;

public static class MockUserRepository
{
    public static IUserRepositoryMock Create(params User[] seededUsers)
    {
        var mock = IUserRepository.Mock();
        var store = seededUsers.ToDictionary(u => u.Id);

        mock.GetByIdAsync(Any<long>(), Any<CancellationToken>())
            .Returns((long id, CancellationToken _) =>
                Task.FromResult(store.GetValueOrDefault(id)));

        mock.GetByUsernameAsync(Any<string>(), Any<CancellationToken>())
            .Returns((string username, CancellationToken _) =>
                Task.FromResult(store.Values.FirstOrDefault(u =>
                    string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))));

        mock.InsertAsync(Any<User>(), Any<CancellationToken>())
            .Returns((User entity, CancellationToken _) =>
            {
                store[entity.Id] = entity;
                return Task.FromResult(entity);
            });

        mock.GetAllAsync(Any<CancellationToken>())
            .Returns((CancellationToken _) =>
                Task.FromResult<IReadOnlyList<User>>(store.Values.ToList()));

        return mock;
    }
}
