using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

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
    public void Sp_RegisterUser_Parameters_Match_Dapper_CallSite()
    {
        var assembly = typeof(UserService.Infrastructure.Repositories.UserRepository).Assembly;
        var sp = ReadEmbeddedSql(assembly, "sp_RegisterUser.sql");
        var spParams = DeclaredParameters(sp);
        Assert.That(spParams, Is.EquivalentTo(new[] { "@Username", "@PasswordHash", "@PasswordSalt" }),
            "sp_RegisterUser parameter contract changed -- the Dapper call site binds exactly these names (SqlException 8144 class, B-2)");
        Assert.That(sp, Does.Contain("OUTPUT INSERTED"), "identity round-trip feeds repository hydration");

        // Call-site sync: every parameters.Add("@X") in the file belongs to an SP header.
        // The file's only DynamicParameters blocks target sp_RegisterUser plus the single
        // @Username of sp_AuthenticateUser, so the union equals the register contract.
        var source = ReadRepoFile("Services", "UserService", "UserService.Infrastructure", "Repositories", "UserRepository.cs");
        var callSiteParams = Regex.Matches(source, @"parameters\.Add\(""@(\w+)""")
            .Select(m => "@" + m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.That(callSiteParams, Is.EquivalentTo(spParams),
            "UserRepository DynamicParameters and SP parameter declarations drifted apart");
    }

    [Test]
    public void Sp_AuthenticateUser_Is_The_Live_Credential_Read()
    {
        var assembly = typeof(UserService.Infrastructure.Repositories.UserRepository).Assembly;
        var sp = ReadEmbeddedSql(assembly, "sp_AuthenticateUser.sql");

        Assert.That(DeclaredParameters(sp), Is.EquivalentTo(new[] { "@Username" }),
            "sp_AuthenticateUser takes exactly @Username (Dapper sends only the bound parameters)");
        Assert.That(sp, Does.Contain("IsDeleted = 0"), "soft-delete predicate guards the credential read (B-1/M-7)");
        Assert.That(sp, Does.Contain("PasswordHash"), "credential columns must be returned (B-1)");
        Assert.That(sp, Does.Contain("PasswordSalt"), "credential columns must be returned (B-1)");

        var source = ReadRepoFile("Services", "UserService", "UserService.Infrastructure", "Repositories", "UserRepository.cs");
        Assert.That(source, Does.Contain("\"sp_AuthenticateUser\""),
            "GetByUsernameAsync must execute sp_AuthenticateUser -- an orphaned SP regresses root-cause cluster #1 (L-4)");
    }

    [Test]
    public void Sp_UpdateNotification_Guards_SoftDelete_With_Exact_Parameters()
    {
        var assembly = typeof(NotificationService.Infrastructure.Repositories.NotificationRepository).Assembly;
        var sp = ReadEmbeddedSql(assembly, "sp_UpdateNotification.sql");
        Assert.That(DeclaredParameters(sp),
            Is.EquivalentTo(new[] { "@Id", "@Title", "@Content", "@Status", "@SentAt", "@UpdatedAt" }));
        Assert.That(sp, Does.Contain("IsDeleted = 0"), "soft-delete guard on update (M-6)");
    }

    [Test]
    public void Sp_InsertNotification_Parameters_Match_Repository_Projection()
    {
        var assembly = typeof(NotificationService.Infrastructure.Repositories.NotificationRepository).Assembly;
        var sp = ReadEmbeddedSql(assembly, "sp_InsertNotification.sql");
        Assert.That(DeclaredParameters(sp),
            Is.EquivalentTo(new[] { "@Title", "@Content", "@Status", "@CreatedAt" }),
            "must mirror the anonymous projection in NotificationRepository.InsertAsync (Dapper binds by name)");
    }

    [Test]
    public void NotificationHistory_Output_Parameters_Are_Declared_And_Bound()
    {
        var migration = ReadRepoFile("Services", "NotificationService", "NotificationService.Infrastructure", "DatabaseMigration.cs");
        Assert.That(migration, Does.Contain("@SPSuccess BIT OUTPUT"));
        Assert.That(migration, Does.Contain("@SPMessage NVARCHAR(255) OUTPUT"));
        Assert.That(migration, Does.Contain("@Entities [dbo].[TypeNotificationHistory] READONLY"));

        var repo = ReadRepoFile("Services", "NotificationService", "NotificationService.Infrastructure", "Repositories", "NotificationHistoryRepository.cs");
        Assert.That(repo, Does.Contain("\"@SPSuccess\""));
        Assert.That(repo, Does.Contain("\"@SPMessage\""));
        Assert.That(repo, Does.Contain("\"@Entities\""));
    }

    [Test]
    public void Migrations_Reference_All_Embedded_Sp_Resources()
    {
        // DatabaseMigration throws "Embedded resource not found" at startup on any drift.
        var userService = ReadRepoFile("Services", "UserService", "UserService.Infrastructure", "DatabaseMigration.cs");
        Assert.That(userService, Does.Contain("sp_RegisterUser.sql"));
        Assert.That(userService, Does.Contain("sp_AuthenticateUser.sql"));

        var notificationService = ReadRepoFile("Services", "NotificationService", "NotificationService.Infrastructure", "DatabaseMigration.cs");
        Assert.That(notificationService, Does.Contain("sp_InsertNotification.sql"));
        Assert.That(notificationService, Does.Contain("sp_UpdateNotification.sql"));
    }

    private static string ReadEmbeddedSql(Assembly assembly, string suffix)
    {
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        Assert.That(resource, Is.Not.Null,
            "Embedded resource '*" + suffix + "' missing from " + assembly.GetName().Name +
            " (csproj EmbeddedResource drift breaks startup migration). Found: " +
            string.Join(", ", assembly.GetManifestResourceNames()));

        using var stream = assembly.GetManifestResourceStream(resource!)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static HashSet<string> DeclaredParameters(string spSql)
    {
        var asKeyword = Regex.Match(spSql, @"\bAS\b");
        Assert.That(asKeyword.Success, Is.True, "SP header not terminated by AS");
        var header = spSql.Substring(0, asKeyword.Index);
        return Regex.Matches(header, @"^\s*(@\w+)\s+(NVARCHAR|VARBINARY|BINARY|BIGINT|INT|BIT|DATETIME2)", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NotificationSystem.slnx")))
            dir = dir.Parent;

        Assert.That(dir, Is.Not.Null,
            "Repository root (NotificationSystem.slnx) not found above " + AppContext.BaseDirectory);
        return dir!.FullName;
    }

    private static string ReadRepoFile(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));
}