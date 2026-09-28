using System.Data;

using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;

using TestDoubles.Helpers;
using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Infrastructure;

/// <summary>
/// Verifies the SQL contract built by <see cref="NotificationCommandFactory"/> — the observable
/// unit, because Dapper's query methods are static extension methods that no test double can
/// intercept. Row mapping and the one-line execute call are not covered here by design.
/// </summary>
public class NotificationRepositoryTests
{
    private static Notification AnEntity(long id = 5)
        => Notification.Rehydrate(id, "Title", "Content", NotificationStatus.Draft,
            createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Test]
    public async Task Constructor_NullGuards()
    {
        var db = TestConnectionFactory.CreatePlainConnection();

        await Assert.That(() => new NotificationRepository(null!, MockUnitOfWork.Create().Object))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("connection");
        await Assert.That(() => new NotificationRepository(db.Connection, null!))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("unitOfWork");
    }

    [Test]
    public async Task GetByIdsAsync_WhenIdsEmpty_ReturnsEmptyListWithoutDbCall()
    {
        // Arrange
        var db = TestConnectionFactory.CreatePlainConnection();
        var repository = new NotificationRepository(db.Connection, MockUnitOfWork.Create().Object);

        // Act — short-circuited before any command is built
        var result = await repository.GetByIdsAsync([], CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEmpty();
        db.ConnectionMock.CreateCommand().WasNeverCalled();
    }

    [Test]
    public async Task MarkSentBatchAsync_WhenIdsEmpty_ReturnsZeroWithoutDbCall()
    {
        // Arrange
        var db = TestConnectionFactory.CreatePlainConnection();
        var repository = new NotificationRepository(db.Connection, MockUnitOfWork.Create().Object);

        // Act
        var result = await repository.MarkSentBatchAsync([], CancellationToken.None);

        // Assert
        await Assert.That(result).IsEqualTo(0);
        db.ConnectionMock.CreateCommand().WasNeverCalled();
    }

    [Test]
    public async Task GetById_Command_FiltersDeletedAndUsesTheId()
    {
        var cmd = NotificationCommandFactory.GetNotificationById(42, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("FROM Notifications");
        await Assert.That(cmd.CommandText).Contains("WHERE Id = @Id");
        await Assert.That(cmd.CommandText).Contains("IsDeleted = 0");
        await Assert.That(cmd.CommandText).DoesNotContain("DELETE FROM");
    }

    [Test]
    public async Task GetByIds_Command_UsesInClauseWithTheFullProjection()
    {
        var cmd = NotificationCommandFactory.GetNotificationsByIds([1L, 2L], null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("WHERE Id IN @Ids");
        await Assert.That(cmd.CommandText).Contains("IsDeleted = 0");
        await AssertProjectionCompleteAsync(cmd.CommandText);
    }

    [Test]
    public async Task GetByIds_Command_IsByteIdenticalToThePreRefactorStatement()
    {
        // R-04 routed this builder through SqlCommands.SelectIn, so the statement is now composed
        // from a shared prefix rather than a hand-written string. This is a *literal*, deliberately
        // not built from NotificationCommandFactory.NotificationColumns: an assertion assembled
        // from the same constant the code uses proves nothing. R-04 acceptance required proving
        // byte equality, not eyeballing it.
        var cmd = NotificationCommandFactory.GetNotificationsByIds([1L, 2L], null, CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo(
            "SELECT Id, Title, Content, Status, SentAt, CreatedAt, UpdatedAt " +
            "FROM Notifications WHERE Id IN @Ids AND IsDeleted = 0");
    }

    [Test]
    public async Task GetByIds_Command_RejectsAnInjectedKeyColumn()
    {
        // The reason GetNotificationsByIds now goes through SqlCommands: the identifiers are
        // interpolated, so they must be validated. This is a direct SqlCommands test — the factory
        // passes literals, so it cannot pass an injected column itself.
        var error = Assert.Throws<ArgumentException>(
            () => Shared.Infrastructure.Persistence.SqlCommands.SelectIn(
                "Notifications",
                ["Id"],
                new { Ids = new long[] { 1 } },
                keyColumn: "Id; DROP TABLE Notifications--",
                parameterName: "Ids",
                transaction: null,
                CancellationToken.None));

        await Assert.That(error.ParamName).IsEqualTo("keyColumn");
    }

    [Test]
    public async Task GetAll_Command_FiltersDeletedAndOmitsParameters()
    {
        var cmd = NotificationCommandFactory.GetAllNotifications(null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("FROM Notifications");
        await Assert.That(cmd.CommandText).Contains("WHERE IsDeleted = 0");
        await Assert.That(cmd.Parameters).IsNull();
    }

    [Test]
    public async Task Insert_Command_IsStoredProcedureWithScalarStatus()
    {
        // Status must be sent as the numeric enum, not the enum name
        var cmd = NotificationCommandFactory.InsertNotification(AnEntity(), null, CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo("SPInsertNotification");
        await Assert.That(cmd.CommandType).IsEqualTo(CommandType.StoredProcedure);
        var names = CommandParameterNames.Of(cmd);
        await Assert.That(names).Contains("@Title");
        await Assert.That(names).Contains("@Content");
        await Assert.That(names).Contains("@Status");
        await Assert.That(names).Contains("@CreatedAt");
    }

    [Test]
    public async Task Insert_Command_IsStoredProcedureCarryingCreatedAt()
    {
        // The repository throws when the SP returns no row: the insert did not happen and
        // the caller must not receive a null entity. Asserting the throw itself needs a
        // live provider, so this pins the command-side half of that contract.
        var cmd = NotificationCommandFactory.InsertNotification(AnEntity(), null, CancellationToken.None);

        await Assert.That(cmd.CommandType).IsEqualTo(CommandType.StoredProcedure);
        await Assert.That(CommandParameterNames.Of(cmd)).Contains("@CreatedAt");
    }

    [Test]
    public async Task Update_Command_IsStoredProcedureCarryingIdentityAndStatus()
    {
        var entity = AnEntity(9);
        var cmd = NotificationCommandFactory.UpdateNotification(entity, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo("SPUpdateNotification");
        await Assert.That(cmd.CommandType).IsEqualTo(CommandType.StoredProcedure);
        var names = CommandParameterNames.Of(cmd);
        await Assert.That(names).Contains("@Id");
        await Assert.That(names).Contains("@Title");
        await Assert.That(names).Contains("@Content");
        await Assert.That(names).Contains("@Status");
        await Assert.That(names).Contains("@SentAt");
        await Assert.That(names).Contains("@UpdatedAt");
    }

    [Test]
    public async Task Update_Command_CarriesIdSoAConcurrentDeleteIsDetectable()
    {
        // The concurrent-delete race: the row vanished between load and update, so
        // ExecuteAsync returns 0 and the repository raises NotFoundException. The @Id the
        // command carries is what makes that race detectable.
        var cmd = NotificationCommandFactory.UpdateNotification(AnEntity(9), null, CancellationToken.None);

        await Assert.That(CommandParameterNames.Of(cmd)).Contains("@Id");
    }

    [Test]
    public async Task MarkSentBatch_Command_GuardsOnDraftStatusAndSoftDelete()
    {
        // M-4: one set-based UPDATE. The Draft guard is what makes a re-send idempotent.
        var cmd = NotificationCommandFactory.MarkSentBatch([1L, 2L], null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("UPDATE Notifications SET Status = @SentStatus");
        await Assert.That(cmd.CommandText).Contains("WHERE Id IN @Ids");
        await Assert.That(cmd.CommandText).Contains("Status = @DraftStatus");
        await Assert.That(cmd.CommandText).Contains("IsDeleted = 0");
        var names = CommandParameterNames.Of(cmd);
        await Assert.That(names).Contains("@Ids");
        await Assert.That(names).Contains("@SentStatus");
        await Assert.That(names).Contains("@DraftStatus");
    }

    [Test]
    public async Task Delete_Command_IsASoftDeleteNeverAPhysicalDelete()
    {
        var cmd = NotificationCommandFactory.SoftDeleteNotification(3, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("SET IsDeleted = 1");
        await Assert.That(cmd.CommandText).Contains("WHERE Id = @Id");
        await Assert.That(cmd.CommandText).Contains("IsDeleted = 0");
        await Assert.That(cmd.CommandText).DoesNotContain("DELETE FROM");
    }

    [Test]
    public async Task Delete_Command_IsGuardedSoARepeatDeleteIsANoOp()
    {
        // ExecuteAsync returns 0 for an already-deleted row; the repository maps >0 to true.
        // The IsDeleted = 0 guard is what makes a repeat delete a no-op.
        var cmd = NotificationCommandFactory.SoftDeleteNotification(3, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).Contains("IsDeleted = 0");
    }

    [Test]
    public async Task GetAll_Command_ProjectsEveryColumnAndFiltersDeleted()
    {
        var cmd = NotificationCommandFactory.GetAllNotifications(null, CancellationToken.None);

        await AssertProjectionCompleteAsync(cmd.CommandText);
        await Assert.That(cmd.CommandText).Contains("WHERE IsDeleted = 0");
    }

    [Test]
    public async Task Commands_PropagateTransactionAndCancellation()
    {
        using var cts = new CancellationTokenSource();
        var db = TestConnectionFactory.CreatePlainConnection();

        var cmd = NotificationCommandFactory.GetNotificationById(1, db.Transaction, cts.Token);

        await Assert.That(cmd.Transaction).IsNotNull();
        await Assert.That(cmd.CancellationToken).IsEqualTo(cts.Token);
    }

    /// <summary>
    /// Asserts the SELECT projection names every column the hydration DTO needs.
    /// Asserting against the <c>NotificationColumns</c> list is NOT enough — mutating that list
    /// to drop <c>UpdatedAt</c> would leave such an assertion green, and a dropped column
    /// silently maps to a default on the entity. Name the columns explicitly (learning 157).
    /// </summary>
    private static async Task AssertProjectionCompleteAsync(string sql)
    {
        var required = new[] { "Id", "Title", "Content", "SentAt", "CreatedAt", "UpdatedAt" };
        var select = sql[
            (sql.IndexOf("SELECT", StringComparison.Ordinal) + 6)..sql.IndexOf(" FROM", StringComparison.Ordinal)];

        foreach (var column in required)
            await Assert.That(select).Contains(column);
    }

    [Test]
    public async Task HydrationRow_ColumnNames_MatchTheProjectionExactly()
    {
        // R-07: NotificationRepository hydrates through a private NotificationRow + Rehydrate
        // instead of relying on Dapper materialising Notification's non-public constructor. Dapper
        // maps by name and silently assigns default for a name it cannot find, so a typo on
        // either side of this seam produces a half-populated entity and no error.
        //
        // Both sets are named explicitly rather than read from the production constant, so
        // mutating NotificationColumns or the row DTO cannot leave this green.
        var rowMembers = new[] { "Id", "Title", "Content", "Status", "SentAt", "CreatedAt", "UpdatedAt" };
        var projected = NotificationCommandFactory.NotificationColumns;

        await Assert.That(projected.OrderBy(c => c, StringComparer.Ordinal))
            .IsEquivalentTo(rowMembers.OrderBy(c => c, StringComparer.Ordinal))
            .Because("Every NotificationRow member must be selected, and nothing else may be: a missing column maps to default, an extra one has no setter");

        // Status is a domain enum mapped from an int column; the row DTO must keep it non-nullable
        // so a missing Status surfaces as 0 (Draft) rather than a null-reference.
        var statusProperty = typeof(NotificationService.Domain.Entities.Notification)
            .GetProperty("Status");
        await Assert.That(statusProperty?.PropertyType)
            .IsEqualTo(typeof(NotificationService.Domain.ValueObjects.NotificationStatus))
            .Because("The row DTO's Status type must match the entity's exactly for Dapper to map it");
    }
}