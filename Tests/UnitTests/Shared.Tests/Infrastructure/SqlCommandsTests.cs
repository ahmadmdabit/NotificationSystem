using Shared.Infrastructure.Persistence;

using TestDoubles.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Infrastructure;

/// <summary>
/// Covers the shared SQL patterns and, critically, the identifier validation. Table and
/// column names are interpolated into SQL and therefore cannot be parameterised, so the
/// validation is the only thing standing between a call-site typo and an injection.
/// </summary>
public class SqlCommandsTests
{
    private static readonly string[] Columns = ["Id", "Title", "Status"];
    private static readonly string[] IdKey = ["Id"];

    [Test]
    public async Task SelectByKey_BuildsProjectionAndKeyPredicate()
    {
        var cmd = SqlCommands.SelectByKey("Notifications", Columns, new { Id = 7 }, IdKey, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo(
            "SELECT Id, Title, Status FROM Notifications WHERE Id = @Id AND IsDeleted = 0");
        await Assert.That(cmd.Parameters).IsNotNull();
    }

    [Test]
    public async Task SelectAll_OmitsWhereClauseOnKeys()
    {
        var cmd = SqlCommands.SelectAll("Users", Columns, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo("SELECT Id, Title, Status FROM Users WHERE IsDeleted = 0");
        await Assert.That(cmd.Parameters).IsNull();
    }

    [Test]
    public async Task SoftDeleteByKey_SetsTheFlagRatherThanRemovingTheRow()
    {
        var cmd = SqlCommands.SoftDeleteByKey("Users", new { Id = 3 }, IdKey, null, CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo(
            "UPDATE Users SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0");
        await Assert.That(cmd.CommandText).DoesNotContain("DELETE FROM");
    }

    [Test]
    public async Task SelectByKey_WithCompositeKey_BuildsEveryKeyPredicate()
    {
        var cmd = SqlCommands.SelectByKey(
            "NotificationHistories",
            ["NotificationId", "UserId"],
            new { NotificationId = 1, UserId = 10 },
            ["NotificationId", "UserId"],
            null,
            CancellationToken.None);

        await Assert.That(cmd.CommandText).IsEqualTo(
            "SELECT NotificationId, UserId FROM NotificationHistories " +
            "WHERE NotificationId = @NotificationId AND UserId = @UserId AND IsDeleted = 0");
    }

    [Test]
    public async Task Commands_PropagateTransactionAndCancellation()
    {
        using var cts = new CancellationTokenSource();
        var db = TestConnectionFactory.CreatePlainConnection();

        var cmd = SqlCommands.SoftDeleteByKey("Users", new { Id = 1 }, IdKey, db.Transaction, cts.Token);

        await Assert.That(cmd.Transaction).IsNotNull();
        await Assert.That(cmd.CancellationToken).IsEqualTo(cts.Token);
    }

    // ---- Identifier validation ----------------------------------------------------

    [Test]
    [Arguments("Users; DROP TABLE Users")]
    [Arguments("Users--")]
    [Arguments("Users/*x*/")]
    [Arguments("Users Name")]
    [Arguments("'Users'")]
    [Arguments("[Users]")]
    [Arguments("1Users")]
    [Arguments("")]
    [Arguments("Us ers")]
    [Arguments("Users;")]
    [Arguments(" Users")]
    [Arguments("Users ")]
    [Arguments("Users\n")]
    [Arguments("Users\r\n")]
    [Arguments("Users\t")]
    public async Task ValidateIdentifier_RejectsAnythingThatIsNotABareIdentifier(string candidate)
    {
        await Assert.That(() => SqlCommands.ValidateIdentifier(candidate, "table"))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    [Arguments("Users")]
    [Arguments("_Users")]
    [Arguments("T")]
    [Arguments("Users2")]
    [Arguments("NotificationHistories")]
    public async Task ValidateIdentifier_AcceptsABareIdentifier(string candidate)
    {
        await Assert.That(SqlCommands.ValidateIdentifier(candidate, "table")).IsEqualTo(candidate);
    }

    [Test]
    public async Task ValidateIdentifier_RejectsNull()
    {
        await Assert.That(() => SqlCommands.ValidateIdentifier(null!, "table"))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task TableName_IsValidatedOnEveryCommand()
    {
        const string malicious = "Users; DROP TABLE Users";

        await Assert.That(() => SqlCommands.SelectAll(malicious, Columns, null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(() => SqlCommands.SoftDeleteByKey(malicious, new { Id = 1 }, IdKey, null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(() => SqlCommands.SelectByKey(malicious, Columns, new { Id = 1 }, IdKey, null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ColumnName_IsValidatedAndCannotSmuggleAFreeTextFragment()
    {
        // The injection vector that a "pass the whole SELECT list in as a string" design
        // would open: the caller cannot inject SQL through a column name.
        var malicious = new[] { "Id", "1 FROM Users; DROP TABLE Users--" };

        await Assert.That(() => SqlCommands.SelectAll("Users", malicious, null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task KeyColumn_IsValidatedSoNoPredicateCanBeInjected()
    {
        var maliciousKey = new[] { "Id = @Id OR 1=1 --" };

        await Assert.That(() => SqlCommands.SoftDeleteByKey("Users", new { Id = 1 }, maliciousKey, null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task EmptyColumnOrKeyList_IsRejected()
    {
        await Assert.That(() => SqlCommands.SelectAll("Users", [], null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
        await Assert.That(() => SqlCommands.SoftDeleteByKey("Users", new { Id = 1 }, [], null, CancellationToken.None))
            .ThrowsExactly<ArgumentException>();
    }
}