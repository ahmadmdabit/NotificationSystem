using NotificationService.Domain;
using NotificationService.Domain.Abstractions;

namespace TestDoubles.Mocks;

public static class MockNotificationHistoryRepository
{
    public static INotificationHistoryRepositoryMock Create(params NotificationHistory[] seededRecords)
    {
        var mock = INotificationHistoryRepository.Mock();
        var store = new Dictionary<(long, long), NotificationHistory>();

        foreach (var record in seededRecords)
        {
            store[(record.NotificationId, record.UserId)] = record;
        }

        mock.GetByIdAsync(Any<long>(), Any<long>(), Any<CancellationToken>())
            .Returns((long notificationId, long userId, CancellationToken _) =>
            {
                store.TryGetValue((notificationId, userId), out var record);
                return Task.FromResult(record);
            });

        mock.GetAllAsync(Any<CancellationToken>())
            .Returns((CancellationToken _) =>
                Task.FromResult<IReadOnlyList<NotificationHistory>>(store.Values.ToList()));

        mock.InsertAsync(Any<NotificationHistory>(), Any<CancellationToken>())
            .Returns((NotificationHistory entity, CancellationToken _) =>
            {
                store[(entity.NotificationId, entity.UserId)] = entity;
                return Task.FromResult(entity);
            });

        mock.InsertBulkAsync(Any<IReadOnlyList<NotificationHistory>>(), Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationHistory> entities, CancellationToken _) =>
            {
                foreach (var entity in entities)
                {
                    store[(entity.NotificationId, entity.UserId)] = entity;
                }
                return Task.FromResult<IReadOnlyList<NotificationHistory>>(entities.ToList());
            });

        mock.DeleteAsync(Any<long>(), Any<long>(), Any<CancellationToken>())
            .Returns((long notificationId, long userId, CancellationToken _) =>
            {
                var removed = store.Remove((notificationId, userId));
                return Task.FromResult(removed);
            });

        return mock;
    }
}
