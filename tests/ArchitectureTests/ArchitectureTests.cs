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
        System.Reflection.Assembly.Load("NotificationService.Api")
    ).Build();

    [Test]
    public void Domain_Must_Not_Depend_On_Application_Infrastructure_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly("UserService.Domain")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("UserService.Application")
                    .Or().ResideInAssembly("UserService.Infrastructure")
                    .Or().ResideInAssembly("UserService.Api"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void Application_Must_Only_Depend_On_Domain_And_Shared()
    {
        IArchRule rule = Types().That().ResideInAssembly("UserService.Application")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("UserService.Infrastructure")
                    .Or().ResideInAssembly("UserService.Api"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void Infrastructure_Must_Depend_On_Application_And_Not_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly("UserService.Infrastructure")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("UserService.Api"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void Api_Must_Only_Depend_On_Application_And_Infrastructure()
    {
        IArchRule rule = Types().That().ResideInAssembly("UserService.Api")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("UserService.Domain"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationDomain_Must_Not_Depend_On_Application_Infrastructure_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly("NotificationService.Domain")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("NotificationService.Application")
                    .Or().ResideInAssembly("NotificationService.Infrastructure")
                    .Or().ResideInAssembly("NotificationService.Api"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationApplication_Must_Only_Depend_On_Domain_And_Shared()
    {
        IArchRule rule = Types().That().ResideInAssembly("NotificationService.Application")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("NotificationService.Infrastructure")
                    .Or().ResideInAssembly("NotificationService.Api"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationInfrastructure_Must_Depend_On_Application_And_Not_Api()
    {
        IArchRule rule = Types().That().ResideInAssembly("NotificationService.Infrastructure")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("NotificationService.Api"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }

    [Test]
    public void NotificationApi_Must_Only_Depend_On_Application_And_Infrastructure()
    {
        IArchRule rule = Types().That().ResideInAssembly("NotificationService.Api")
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly("NotificationService.Domain"))
            .WithoutRequiringPositiveResults();
        rule.Check(Architecture);
    }
}
