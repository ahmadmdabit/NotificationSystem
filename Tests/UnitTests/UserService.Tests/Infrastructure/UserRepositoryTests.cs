using System.Data;
using System.Data.Common;

using Dapper;

using TestDoubles.Helpers;
using TestDoubles.Mocks;
using TestDoubles.Stubs;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Domain.Entities;
using UserService.Infrastructure.Persistence;
using UserService.Infrastructure.Repositories;

namespace UserService.Tests.Infrastructure;

/// <summary>
/// Verifies the SQL contract issued by <see cref="UserRepository"/>.
/// </summary>
/// <remarks>
/// <para>These tests target <see cref="UserCommandFactory"/> directly rather than driving the
/// repository through a mocked connection. Dapper's <c>QuerySingleOrDefaultAsync</c> and
/// <c>ExecuteAsync</c> are <b>static extension methods</b> on <see cref="IDbConnection"/>, so a
/// source-generated connection mock cannot intercept them; and the generated
/// <see cref="DbCommand"/> mock exposes neither <c>CreateParameter()</c> nor <c>Parameters</c>
/// (both protected), so Dapper throws a <see cref="NullReferenceException"/> before executing.
/// The builders are pure, so asserting on them covers the part that carries real risk — the
/// command text, the command type, and the stored-procedure parameter names.</para>
/// <para><b>Not covered:</b> row mapping and the one-line <c>_connection.XxxAsync(cmd)</c> call,
/// both of which need a live ADO.NET provider.</para>
/// </remarks>
public class UserRepositoryTests
{
    private static User NewUser(string username = "TestUser")
        => User.Create(username, "Password123", new SyncHasher());

    /// <summary>
    /// Parameter names bound by a command. Dapper's <c>DynamicParameters</c> is not enumerable in
    /// Dapper 2.1.86 and its <c>ParamInfo</c> is internal, so <c>ParameterNames</c> is the public
    /// way in. Anonymous-object parameters (the read queries) are reflected instead.
    /// </summary>
    private static string[] ParamNamesOf(CommandDefinition command)
        => [.. CommandParameterNames.Of(command)];

    private static object? ParamValueOf(CommandDefinition command, string name)
    {
        if (command.Parameters is DynamicParameters parameters)
            return parameters.Get<object?>(name);

        var anonymous = command.Parameters!;
        return anonymous.GetType()
            .GetProperty(name.TrimStart('@'))!
            .GetValue(anonymous);
    }

    [Test]
    public async Task GetById_SelectsOnlyNonDeletedRows_AndFiltersById()
    {
        var command = UserCommandFactory.GetById(42, null, CancellationToken.None);

        await Assert.That(command.CommandText).Contains("FROM Users");
        await Assert.That(command.CommandText).Contains("WHERE Id = @Id");
        await Assert.That(command.CommandText).Contains("IsDeleted = 0");
        await Assert.That(ParamValueOf(command, "@Id")).IsEqualTo(42L);
    }

    [Test]
    public async Task GetById_ProjectsEveryColumnRequiredForHydration()
    {
        var command = UserCommandFactory.GetById(1, null, CancellationToken.None);

        // UserRow needs each of these; a projection that drops one silently breaks hydration.
        foreach (var column in new[] { "Id", "Username", "PasswordHash", "PasswordSalt", "CreatedAt", "UpdatedAt" })
        {
            await Assert.That(command.CommandText).Contains(column);
        }
    }

    [Test]
    public async Task GetByUsername_CallsStoredProcedure_WithUsernameParameter()
    {
        var command = UserCommandFactory.GetByUsername("Alice", null, CancellationToken.None);

        await Assert.That(command.CommandText).IsEqualTo("SPAuthenticateUser");
        await Assert.That(command.CommandType).IsEqualTo(CommandType.StoredProcedure);
        await Assert.That(ParamNamesOf(command)).Contains("@Username");
        await Assert.That(ParamValueOf(command, "@Username")).IsEqualTo("Alice");
    }


    [Test]
    public async Task Insert_CallsRegisterUser_WithParameterNamesMatchingTheStoredProcedure()
    {
        var user = NewUser("NewUser");

        var command = UserCommandFactory.Insert(user, null, CancellationToken.None);

        await Assert.That(command.CommandText).IsEqualTo("SPRegisterUser");
        await Assert.That(command.CommandType).IsEqualTo(CommandType.StoredProcedure);

        // Regression guard: an anonymous projection emits @Hash/@Salt and fails with SqlException 8144.
        var names = ParamNamesOf(command);
        await Assert.That(names).Contains("@Username");
        await Assert.That(names).Contains("@PasswordHash");
        await Assert.That(names).Contains("@PasswordSalt");
        await Assert.That(names).DoesNotContain("@Hash");
        await Assert.That(names).DoesNotContain("@Salt");
    }

    [Test]
    public async Task Insert_BindsUsernameAndBothCredentialBuffers()
    {
        var user = NewUser();

        var command = UserCommandFactory.Insert(user, null, CancellationToken.None);

        await Assert.That(ParamValueOf(command, "@Username")).IsEqualTo(user.Username);
        await Assert.That(ParamValueOf(command, "@PasswordHash")).IsNotNull();
        await Assert.That(ParamValueOf(command, "@PasswordSalt")).IsNotNull();
    }

    [Test]
    public async Task Delete_PerformsSoftDelete_AndNeverRemovesTheRow()
    {
        var command = UserCommandFactory.Delete(7, null, CancellationToken.None);

        await Assert.That(command.CommandText).Contains("IsDeleted = 1");
        await Assert.That(command.CommandText).Contains("SYSUTCDATETIME()");
        await Assert.That(command.CommandText).Contains("WHERE Id = @Id AND IsDeleted = 0");

        // A hard DELETE here would destroy audit history.
        await Assert.That(command.CommandText).DoesNotContain("DELETE FROM");
        await Assert.That(ParamValueOf(command, "@Id")).IsEqualTo(7L);
    }

    [Test]
    public async Task GetAll_ExcludesDeletedRows_AndTakesNoParameters()
    {
        var command = UserCommandFactory.GetAll(null, CancellationToken.None);

        await Assert.That(command.CommandText).Contains("FROM Users");
        await Assert.That(command.CommandText).Contains("IsDeleted = 0");
        await Assert.That(command.Parameters).IsNull();
    }

    [Test]
    public async Task Commands_PropagateTheAmbientTransaction()
    {
        IDbTransaction transaction = TestTransactionFactory.CreateAmbient();

        await Assert.That(UserCommandFactory.GetById(1, transaction, CancellationToken.None).Transaction)
            .IsSameReferenceAs(transaction);
        await Assert.That(UserCommandFactory.GetByUsername("a", transaction, CancellationToken.None).Transaction)
            .IsSameReferenceAs(transaction);
        await Assert.That(UserCommandFactory.Insert(NewUser(), transaction, CancellationToken.None).Transaction)
            .IsSameReferenceAs(transaction);
        await Assert.That(UserCommandFactory.Delete(1, transaction, CancellationToken.None).Transaction)
            .IsSameReferenceAs(transaction);
        await Assert.That(UserCommandFactory.GetAll(transaction, CancellationToken.None).Transaction)
            .IsSameReferenceAs(transaction);
    }

    [Test]
    public async Task Commands_PropagateTheCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await Assert.That(UserCommandFactory.GetById(1, null, cts.Token).CancellationToken)
            .IsEqualTo(cts.Token);
        await Assert.That(UserCommandFactory.Delete(1, null, cts.Token).CancellationToken)
            .IsEqualTo(cts.Token);
    }

    [Test]
    public async Task Repository_RequiresBothConnectionAndUnitOfWork()
    {
        var db = TestConnectionFactory.CreatePlainConnection();
        var unitOfWork = MockUnitOfWork.Create();

        await Assert.That(() => new UserRepository(null!, unitOfWork.Object))
            .ThrowsExactly<ArgumentNullException>().WithParameterName("connection");
        await Assert.That(() => new UserRepository(db.Connection, null!))
            .ThrowsExactly<ArgumentNullException>().WithParameterName("unitOfWork");
    }
}
