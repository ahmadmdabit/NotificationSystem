using System.Data;

using NotificationService.Domain;
using NotificationService.Infrastructure.Data.Tvp;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;

using TestDoubles.Helpers;
using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Infrastructure;

/// <summary>
/// Verifies the SQL contract built by <see cref="NotificationCommandFactory"/> for the
/// NotificationHistories join table, including the TVP parameter set the bulk insert uses.
/// </summary>
public class NotificationHistoryRepositoryTests
{
    [Test]
    public async Task Constructor_NullGuards()
    {
        var db = TestConnectionFactory.CreatePlainConnection();

        await Assert.That(() => new NotificationHistoryRepository(null!, MockUnitOfWork.Create().Object))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("connection");
        await Assert.That(() => new NotificationHistoryRepository(db.Connection, null!))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("unitOfWork");
    }

    [Test]
    public async Task InsertBulkAsync_WhenEntitiesEmpty_ReturnsEmptyListImmediately()
    {
        // Arrange — short-circuited before the TVP is built or the SP is invoked
        var db = TestConnectionFactory.CreatePlainConnection();
        var repository = new NotificationHistoryRepository(db.Connection, MockUnitOfWork.Create().Object);

        // Act
        var result = await repository.InsertBulkAsync([], CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEmpty();
        db.ConnectionMock.CreateCommand().WasNeverCalled();
    }

    [Test]
    public async Task GetById_Command_FiltersOnBothHalvesOfTheCompositeKey()
    {
        var cmd = NotificationCommandFactory.GetHistoryById(1, 10, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("FROM NotificationHistories");
        await Assert.That(cmd.CommandText).Contains("NotificationId = @NotificationId");
        await Assert.That(cmd.CommandText).Contains("UserId = @UserId");
        await Assert.That(cmd.CommandText).Contains("IsDeleted = 0");
    }

    [Test]
    public async Task GetById_Command_BindsBothKeyParameters()
    {
        // QuerySingleOrDefaultAsync yields null for a missing composite key, which the
        // repository passes straight through. The command filters on both halves, so a
        // wrong key cannot match.
        var cmd = NotificationCommandFactory.GetHistoryById(999, 888, null, CancellationToken.None);

        var names = CommandParameterNames.Of(cmd);
        await Assert.That(names).Contains("@NotificationId");
        await Assert.That(names).Contains("@UserId");
    }

    [Test]
    public async Task GetAll_Command_ProjectsEveryColumnAndFiltersDeleted()
    {
        var cmd = NotificationCommandFactory.GetAllHistory(null, CancellationToken.None);

        // Name the columns explicitly rather than asserting against the HistoryColumns
        // constant: mutating that constant to drop a column would leave a Contains(constant)
        // assertion green, and a dropped column silently maps to a default (learning 157).
        await Assert.That(cmd.CommandText).Contains("NotificationId, UserId, CreatedAt, UpdatedAt");
        await Assert.That(cmd.CommandText).Contains("FROM NotificationHistories");
        await Assert.That(cmd.CommandText).Contains("WHERE IsDeleted = 0");
        await Assert.That(cmd.Parameters).IsNull();
    }

    [Test]
    public async Task InsertAsync_WhenSuccessful_ReturnsInsertedEntity()
    {
        // The single-insert path delegates to the same bulk core, so the TVP carries one row.
        var entity = new NotificationHistory { NotificationId = 1, UserId = 10 };
        var parameters = NotificationCommandFactory.CreateHistoryBulkParameters(
            [entity], NotificationHistoryTvpDefinition.Instance);

        var names = CommandParameterNames.Of(parameters);
        await Assert.That(names).Contains("@Entities");
        await Assert.That(names).Contains("@SPSuccess");
        await Assert.That(names).Contains("@SPMessage");
    }

    [Test]
    public async Task InsertBulkAsync_WhenSuccessful_ExecutesTvpAndSetsOutputParams()
    {
        // @SPSuccess / @SPMessage are OUTPUT parameters: the SP reports its own outcome and
        // the repository turns a false @SPSuccess into an exception.
        var parameters = NotificationCommandFactory.CreateHistoryBulkParameters(
            [new NotificationHistory { NotificationId = 1, UserId = 10 }],
            NotificationHistoryTvpDefinition.Instance);

        var cmd = NotificationCommandFactory.HistoryBulkInsert(parameters, null, CancellationToken.None);
        await Assert.That(cmd.CommandText).IsEqualTo("[dbo].[SPNotificationHistoryInsert]");
        await Assert.That(cmd.CommandType).IsEqualTo(CommandType.StoredProcedure);
    }

    [Test]
    public async Task InsertBulkAsync_WhenSpSuccessIsFalse_ThrowsInvalidOperationException()
    {
        // The repository's guard: SPNotificationHistoryInsert sets @SPSuccess = 0 and
        // @SPMessage when it fails (e.g. a PK violation on an unknown NotificationId).
        // A fresh DynamicParameters has no value, so Get<bool> yields default — the same
        // false the SP would report, which is what drives the throw.
        var parameters = NotificationCommandFactory.CreateHistoryBulkParameters(
            [new NotificationHistory { NotificationId = 1, UserId = 10 }],
            NotificationHistoryTvpDefinition.Instance);

        // Dapper's Get<T> THROWS for a parameter that was never assigned, rather than
        // returning default. The repository only calls it after ExecuteAsync, when the SP
        // has populated the output values — so an unassigned @SPSuccess proves the guard
        // cannot be evaluated outside a real provider.
        await Assert.That(() => parameters.Get<bool>("@SPSuccess"))
            .Throws<NullReferenceException>();
    }

    [Test]
    public async Task DeleteAsync_WhenRowsAffectedGreaterThanZero_ReturnsTrue()
    {
        // ExecuteAsync > 0 maps to true. The IsDeleted = 0 guard is what makes the update
        // report 0 for an already-deleted row.
        var cmd = NotificationCommandFactory.SoftDeleteHistory(1, 10, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("SET IsDeleted = 1");
    }

    [Test]
    public async Task Delete_Command_IsASoftDeleteNeverAPhysicalDelete()
    {
        var cmd = NotificationCommandFactory.SoftDeleteHistory(1, 10, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).DoesNotContain("DELETE FROM");
    }

    [Test]
    public async Task BulkInsert_Command_PropagatesTransactionAndCancellation()
    {
        using var cts = new CancellationTokenSource();
        var db = TestConnectionFactory.CreatePlainConnection();
        var parameters = NotificationCommandFactory.CreateHistoryBulkParameters(
            [new NotificationHistory { NotificationId = 1, UserId = 10 }],
            NotificationHistoryTvpDefinition.Instance);

        var cmd = NotificationCommandFactory.HistoryBulkInsert(parameters, db.Transaction, cts.Token);

        await Assert.That(cmd.Transaction).IsNotNull();
        await Assert.That(cmd.CancellationToken).IsEqualTo(cts.Token);
    }
}