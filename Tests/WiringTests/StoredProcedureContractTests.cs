using System.Reflection;
using System.Text.RegularExpressions;

using TUnit.Assertions;
using TUnit.Assertions.Exceptions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace WiringTests;

/// <summary>
/// SQL-template and stored-procedure contract tests (L-12): run against the embedded SP
/// resources and repository source text -- no database required. Pins the call-site/SP
/// parameter contracts behind SqlException 8144 (B-2), the orphaned-SP defect (L-4 /
/// root-cause cluster #1) and the soft-delete guards (B-3/M-6/M-7).
/// </summary>
public class StoredProcedureContractTests
{
    [Test]
    public async Task SPRegisterUser_Parameters_Match_Dapper_CallSite()
    {
        var assembly = typeof(UserService.Infrastructure.Repositories.UserRepository).Assembly;
        var sp = await ReadEmbeddedSql(assembly, "SPRegisterUser.sql");
        var spParams = await DeclaredParameters(sp);
        await Assert.That(spParams).IsEquivalentTo(new[] { "@Username", "@PasswordHash", "@PasswordSalt" })
            .Because("SPRegisterUser parameter contract changed -- the Dapper call site binds exactly these names (SqlException 8144 class, B-2)");
        await Assert.That(sp).Contains("OUTPUT INSERTED")
            .Because("identity round-trip feeds repository hydration");

        // Call-site sync: every parameters.Add("@X") bound anywhere in UserService.Infrastructure
        // belongs to an SP header. The only DynamicParameters blocks target SPRegisterUser plus
        // the single @Username of SPAuthenticateUser, so the union equals the register contract.
        //
        // Scanned project-wide, not per-file: the SQL contract was extracted from UserRepository
        // into Persistence/UserCommandFactory (the Dapper extension methods cannot be mocked, so
        // the command builders became the testable unit). Pinning a single file here broke the
        // test the moment the code moved; the contract being guarded is "the bound parameter names
        // match the SP header", wherever the builder lives.
        var source = await ReadProjectSource("Services", "UserService", "UserService.Infrastructure");
        var callSiteParams = Regex.Matches(source, @"parameters\.Add\(""@(\w+)""")
            .Select(m => "@" + m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        await Assert.That(callSiteParams).IsEquivalentTo(spParams)
            .Because("UserRepository DynamicParameters and SP parameter declarations drifted apart");
    }

    [Test]
    public async Task SPAuthenticateUser_Is_The_Live_Credential_Read()
    {
        var assembly = typeof(UserService.Infrastructure.Repositories.UserRepository).Assembly;
        var sp = await ReadEmbeddedSql(assembly, "SPAuthenticateUser.sql");

        await Assert.That(await DeclaredParameters(sp)).IsEquivalentTo(new[] { "@Username" })
            .Because("SPAuthenticateUser takes exactly @Username (Dapper sends only the bound parameters)");
        await Assert.That(sp).Contains("IsDeleted = 0")
            .Because("soft-delete predicate guards the credential read (B-1/M-7)");
        await Assert.That(sp).Contains("PasswordHash")
            .Because("credential columns must be returned (B-1)");
        await Assert.That(sp).Contains("PasswordSalt")
            .Because("credential columns must be returned (B-1)");

        // Project-wide scan for the same reason as the call-site sync above: the SP name now lives
        // in Persistence/UserCommandFactory, not UserRepository.
        var source = await ReadProjectSource("Services", "UserService", "UserService.Infrastructure");
        await Assert.That(source).Contains("\"SPAuthenticateUser\"")
            .Because("GetByUsernameAsync must execute SPAuthenticateUser -- an orphaned SP regresses root-cause cluster #1 (L-4)");
    }

    [Test]
    public async Task SPUpdateNotification_Guards_SoftDelete_With_Exact_Parameters()
    {
        var assembly = typeof(NotificationService.Infrastructure.Repositories.NotificationRepository).Assembly;
        var sp = await ReadEmbeddedSql(assembly, "SPUpdateNotification.sql");
        await Assert.That(await DeclaredParameters(sp)).IsEquivalentTo(new[] { "@Id", "@Title", "@Content", "@Status", "@SentAt", "@UpdatedAt" });
        await Assert.That(sp).Contains("IsDeleted = 0")
            .Because("soft-delete guard on update (M-6)");
    }

    [Test]
    public async Task SPInsertNotification_Parameters_Match_Repository_Projection()
    {
        var assembly = typeof(NotificationService.Infrastructure.Repositories.NotificationRepository).Assembly;
        var sp = await ReadEmbeddedSql(assembly, "SPInsertNotification.sql");
        await Assert.That(await DeclaredParameters(sp)).IsEquivalentTo(new[] { "@Title", "@Content", "@Status", "@CreatedAt" })
            .Because("must mirror the anonymous projection in NotificationRepository.InsertAsync (Dapper binds by name)");
    }

    [Test]
    public async Task NotificationHistory_Output_Parameters_Are_Declared_And_Bound()
    {
        var migration = ReadRepoFile("Services", "NotificationService", "NotificationService.Infrastructure", "DatabaseMigration.cs");
        await Assert.That(migration).Contains("@SPSuccess BIT OUTPUT");
        await Assert.That(migration).Contains("@SPMessage NVARCHAR(255) OUTPUT");
        await Assert.That(migration).Contains("@Entities [dbo].[TypeNotificationHistory] READONLY");

        // Scan the whole Infrastructure project, not one file: the binding moved into
        // Persistence/NotificationCommandFactory, and the property under test ("the TVP
        // parameters the repository binds match the SP signature") is a property of the
        // project. Pinning the repository file made this fail on a pure refactor.
        var source = await ReadProjectSource("Services", "NotificationService", "NotificationService.Infrastructure");
        await Assert.That(source).Contains("\"@SPSuccess\"");
        await Assert.That(source).Contains("\"@SPMessage\"");
        await Assert.That(source).Contains("\"@Entities\"");
    }

    [Test]
    public async Task Sp_NotificationHistoryInsert_IsIdempotent_For_A_RepeatPair()
    {
        // N-07. NotificationHistories has PRIMARY KEY (NotificationId, UserId), so the SP insert
        // MUST be filtered against existing rows. Without that guard, sending the same
        // (notification, user) pair twice raised a PK violation inside the SP, which set
        // @SPSuccess = 0, which NotificationHistoryRepository turned into an
        // InvalidOperationException and ApiExceptionHandler into HTTP 500.
        //
        // This is a TEXT guard, not a behavioural one: it proves the guard was not deleted,
        // not that the SP runs correctly. The behavioural proof needs a live database.
        var migration = ReadRepoFile("Services", "NotificationService", "NotificationService.Infrastructure", "DatabaseMigration.cs");

        // Non-vacuity: assert the SP body is present before asserting anything about it.
        await Assert.That(migration).Contains("SPNotificationHistoryInsert")
            .Because("a missing SP would make every assertion below pass for the wrong reason");

        await Assert.That(migration).Contains("WHERE NOT EXISTS")
            .Because("the history insert must skip pairs that already exist, or the composite PK turns a repeat send into a 500 (N-07)");

        await Assert.That(migration).Contains("h.NotificationId = e.NotificationId")
            .Because("the guard must correlate on the composite key, both halves");
        await Assert.That(migration).Contains("h.UserId = e.UserId")
            .Because("a half-key guard would still raise the PK violation on the second column");

        // The composite PK is what makes the guard mandatory; pin it so dropping the key
        // later gets a deliberate look rather than silently changing the SP contract.
        await Assert.That(migration).Contains("PRIMARY KEY ([NotificationId], [UserId])")
            .Because("the NOT EXISTS guard is only correct because the composite key exists");
    }


    [Test]
    public async Task Migrations_Reference_All_Embedded_Sp_Resources()
    {
        // DatabaseMigration throws "Embedded resource not found" at startup on any drift.
        var userService = ReadRepoFile("Services", "UserService", "UserService.Infrastructure", "DatabaseMigration.cs");
        await Assert.That(userService).Contains("SPRegisterUser.sql");
        await Assert.That(userService).Contains("SPAuthenticateUser.sql");

        var notificationService = ReadRepoFile("Services", "NotificationService", "NotificationService.Infrastructure", "DatabaseMigration.cs");
        await Assert.That(notificationService).Contains("SPInsertNotification.sql");
        await Assert.That(notificationService).Contains("SPUpdateNotification.sql");
    }

    private static async Task<string> ReadEmbeddedSql(Assembly assembly, string suffix)
    {
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (resource is null)
            throw new InvalidOperationException(
                "Embedded resource '*" + suffix + "' missing from " + assembly.GetName().Name +
                " (csproj EmbeddedResource drift breaks startup migration). Found: " +
                string.Join(", ", assembly.GetManifestResourceNames()));

        using var stream = assembly.GetManifestResourceStream(resource!)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task<HashSet<string>> DeclaredParameters(string spSql)
    {
        var asKeyword = Regex.Match(spSql, @"\bAS\b");
        if (!asKeyword.Success)
            throw new AssertionException("SP header not terminated by AS");
        var header = spSql.Substring(0, asKeyword.Index);
        return Regex.Matches(header, @"^\s*(@\w+)\s+(NVARCHAR|VARBINARY|BINARY|BIGINT|INT|BIT|DATETIME2)", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<string> RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NotificationSystem.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new AssertionException(
                "Repository root (NotificationSystem.slnx) not found above " + AppContext.BaseDirectory);

        return dir!.FullName;
    }

    private static async Task<string> ReadRepoFile(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { await RepoRoot() }.Concat(parts).ToArray()));

    /// <summary>
    /// Concatenates every <c>.cs</c> file under a project directory, excluding bin/obj.
    /// </summary>
    /// <remarks>
    /// Used by the contract tests that assert on SQL *text*. Those guards describe a property of
    /// the project ("every bound <c>@Parameter</c> matches an SP header"), not of one file, so they
    /// must survive code moving between files — pinning a single path made them fail the moment
    /// the SQL was extracted into <c>Persistence/UserCommandFactory</c>.
    /// </remarks>
    private static async Task<string> ReadProjectSource(params string[] projectParts)
    {
        var dir = Path.Combine(new[] { await RepoRoot() }.Concat(projectParts).ToArray());
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal);
        return string.Join("\n", files.Select(File.ReadAllText));
    }
}