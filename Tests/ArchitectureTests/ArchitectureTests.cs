using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using ArchUnitNET.NUnit;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArchitectureTests;

/// <summary>
/// ArchUnitNET-based architecture tests enforcing Clean Architecture dependency rules.
/// MUST run in Debug configuration (bytecode analysis requires Debug build).
/// Positive-result guards (4.1): WithoutRequiringPositiveResults is deliberately omitted
/// from every rule, so each subject filter must match at least one type -- an emptied
/// assembly or namespace (e.g. all controllers deleted) fails the suite instead of
/// passing vacuously. Assembly predicates use the Assembly-object overload of
/// ResideInAssembly (the string overload matches Assembly.FullName exactly, which a
/// bare name never satisfies -- verified empirically against ArchUnitNET 0.13.4).
/// The framework-leak and kernel rules (4.2) bind against the real loaded framework
/// assemblies (Dapper, SqlClient, MassTransit, System.Data) and always evaluate
/// positively because their subjects are the non-empty Domain assemblies.
/// </summary>
public class ArchitectureTests
{
    private static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(
        System.Reflection.Assembly.Load("UserService.Domain"),
        System.Reflection.Assembly.Load("UserService.Application"),
        System.Reflection.Assembly.Load("UserService.Infrastructure"),
        System.Reflection.Assembly.Load("UserService.Api"),
        System.Reflection.Assembly.Load("NotificationService.Domain"),
        System.Reflection.Assembly.Load("NotificationService.Application"),
        System.Reflection.Assembly.Load("NotificationService.Infrastructure"),
        System.Reflection.Assembly.Load("NotificationService.Api"),
        // Shared kernel: subject of the kernel rule, forbidden target for the layering rules.
        System.Reflection.Assembly.Load("Shared.Domain"),
        System.Reflection.Assembly.Load("Shared.Application"),
        System.Reflection.Assembly.Load("Shared.Infrastructure"),
        System.Reflection.Assembly.Load("Shared.Api"),
        // Framework assemblies: real targets for the Domain framework-leak rules (4.2).
        System.Reflection.Assembly.Load("Dapper"),
        System.Reflection.Assembly.Load("Microsoft.Data.SqlClient"),
        System.Reflection.Assembly.Load("MassTransit"),
        System.Reflection.Assembly.Load("MassTransit.RabbitMqTransport"),
        System.Reflection.Assembly.Load("System.Data.Common")
    ).Build();

    private static System.Reflection.Assembly Asm(string name)
        => System.Reflection.Assembly.Load(name);

    [Test]
    public void Domain_Must_Not_Depend_On_Application_Infrastructure_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("UserService.Domain"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    Asm("UserService.Application"),
                    Asm("UserService.Infrastructure"),
                    Asm("UserService.Api")));
        rule.Check(Architecture);
    }

    [Test]
    public void Application_Must_Only_Depend_On_Domain_And_Shared()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("UserService.Application"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    Asm("UserService.Infrastructure"),
                    Asm("UserService.Api")));
        rule.Check(Architecture);
    }

    [Test]
    public void Infrastructure_Must_Depend_On_Application_And_Not_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("UserService.Infrastructure"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(Asm("UserService.Api")));
        rule.Check(Architecture);
    }

    [Test]
    public void Api_Must_Only_Depend_On_Application_And_Infrastructure()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("UserService.Api"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(Asm("UserService.Domain")));
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationDomain_Must_Not_Depend_On_Application_Infrastructure_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("NotificationService.Domain"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    Asm("NotificationService.Application"),
                    Asm("NotificationService.Infrastructure"),
                    Asm("NotificationService.Api")));
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationApplication_Must_Only_Depend_On_Domain_And_Shared()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("NotificationService.Application"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    Asm("NotificationService.Infrastructure"),
                    Asm("NotificationService.Api")));
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationInfrastructure_Must_Depend_On_Application_And_Not_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("NotificationService.Infrastructure"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(Asm("NotificationService.Api")));
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationApi_Must_Only_Depend_On_Application_And_Infrastructure()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("NotificationService.Api"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(Asm("NotificationService.Domain")));
        rule.Check(Architecture);
    }

    // ---- 4.2 framework-dependency + shared-kernel rules ----------------------

    [Test]
    public void Domains_Must_Not_Depend_On_Data_Or_Messaging_Frameworks()
    {
        foreach (var domainAssembly in new[] { "UserService.Domain", "NotificationService.Domain", "Shared.Domain" })
        {
            IArchRule rule = Types().That().ResideInAssembly(Asm(domainAssembly))
                .Should().NotDependOnAny(
                    Types().That().ResideInAssembly(
                        Asm("Dapper"),
                        Asm("Microsoft.Data.SqlClient"),
                        Asm("MassTransit"),
                        Asm("MassTransit.RabbitMqTransport"),
                        Asm("System.Data.Common"),
                        Asm("Shared.Application"),
                        Asm("Shared.Infrastructure"),
                        Asm("Shared.Api")))
                .Because("Domain layers must stay persistence/messaging agnostic and never reach up into Application, Infrastructure or HTTP");
            rule.Check(Architecture);
        }
    }

    [Test]
    public void SharedDomain_Must_Not_Depend_On_Service_Assemblies()
    {
        IArchRule rule = Types().That().ResideInAssembly(Asm("Shared.Domain"))
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(
                    Asm("UserService.Domain"),
                    Asm("UserService.Application"),
                    Asm("UserService.Infrastructure"),
                    Asm("UserService.Api"),
                    Asm("NotificationService.Domain"),
                    Asm("NotificationService.Application"),
                    Asm("NotificationService.Infrastructure"),
                    Asm("NotificationService.Api")))
            .Because("the shared kernel must not know any concrete service");
        rule.Check(Architecture);
    }

    [Test]
    public void Api_Controllers_Must_Not_Depend_On_Infrastructure()
    {
        IArchRule userControllers = Types().That().ResideInNamespace("UserService.Api.Controllers")
            .Should().NotDependOnAny(Types().That().ResideInAssembly(Asm("UserService.Infrastructure")))
            .Because("controllers must delegate through IMediator (Application), never Infrastructure");
        userControllers.Check(Architecture);

        IArchRule notificationControllers = Types().That().ResideInNamespace("NotificationService.Api.Controllers")
            .Should().NotDependOnAny(Types().That().ResideInAssembly(Asm("NotificationService.Infrastructure")))
            .Because("controllers must delegate through IMediator (Application), never Infrastructure");
        notificationControllers.Check(Architecture);
    }
}