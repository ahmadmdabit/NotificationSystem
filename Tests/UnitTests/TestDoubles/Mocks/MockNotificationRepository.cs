using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;

namespace TestDoubles.Mocks;

public static class MockNotificationRepository
{
    public static INotificationRepositoryMock Create(params Notification[] seededNotifications)
    {
        var mock = INotificationRepository.Mock();
        var store = seededNotifications.ToDictionary(n => n.Id);

        mock.GetByIdAsync(Any<long>(), Any<CancellationToken>())
            .Returns((long id, CancellationToken _) =>
                Task.FromResult(store.GetValueOrDefault(id)));

        mock.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>())
            .Returns((IEnumerable<long> ids, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Notification>>(
                    ids.Where(store.ContainsKey).Select(id => store[id]).ToList()));

        mock.InsertAsync(Any<Notification>(), Any<CancellationToken>())
            .Returns((Notification entity, CancellationToken _) =>
            {
                store[entity.Id] = entity;
                return Task.FromResult(entity);
            });

        mock.UpdateAsync(Any<Notification>(), Any<CancellationToken>())
            .Returns((Notification entity, CancellationToken _) =>
            {
                store[entity.Id] = entity;
                return Task.FromResult(entity);
            });

        mock.GetAllAsync(Any<CancellationToken>())
            .Returns((CancellationToken _) =>
                Task.FromResult<IReadOnlyList<Notification>>(store.Values.ToList()));

        mock.MarkSentBatchAsync(Any<IReadOnlyCollection<long>>(), Any<CancellationToken>())
            .Returns((IReadOnlyCollection<long> ids, CancellationToken _) => ids.Count);

        mock.DeleteAsync(Any<long>(), Any<CancellationToken>())
            .Returns((long id, CancellationToken _) => Task.FromResult(store.Remove(id)));

        return mock;
    }
}
