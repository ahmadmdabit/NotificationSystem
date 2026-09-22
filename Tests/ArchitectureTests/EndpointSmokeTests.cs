using System.Reflection;
using NUnit.Framework;

namespace ArchitectureTests;

/// <summary>
/// Endpoint smoke tests (L-12): reflection-level contract guards over controller route
/// and authorization metadata plus the WebApplicationFactory entry point. Deliberately
/// does not boot the host (DatabaseMigration requires a live SQL Server), so the full
/// HTTP round trip stays with the manual Evidence checklist item.
/// </summary>
public class EndpointSmokeTests
{
    private const string ApiController = "Microsoft.AspNetCore.Mvc.ApiControllerAttribute";
    private const string Route = "Microsoft.AspNetCore.Mvc.RouteAttribute";
    private const string HttpGet = "Microsoft.AspNetCore.Mvc.HttpGetAttribute";
    private const string HttpPost = "Microsoft.AspNetCore.Mvc.HttpPostAttribute";
    private const string HttpPut = "Microsoft.AspNetCore.Mvc.HttpPutAttribute";
    private const string HttpDelete = "Microsoft.AspNetCore.Mvc.HttpDeleteAttribute";
    private const string AllowAnonymous = "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute";
    private const string Authorize = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";

    [Test]
    public void Program_EntryPoints_Are_Public_For_WebApplicationFactory()
    {
        foreach (var assemblyName in new[] { "UserService.Api", "NotificationService.Api" })
        {
            var program = Assembly.Load(assemblyName).GetType("Program");
            Assert.That(program, Is.Not.Null,
                assemblyName + " must declare Program (public partial class Program) for WebApplicationFactory");
            Assert.That(program!.IsPublic, Is.True,
                assemblyName + ".Program must be public so tests can construct WebApplicationFactory<Program>");
        }
    }

    [Test]
    public void UsersController_Route_And_Authorization_Contract()
    {
        var controller = Load("UserService.Api", "UserService.Api.Controllers.UsersController");
        AssertClassContract(controller);

        AssertRoute(Method(controller, "RegisterAsync"), HttpPost, "Register");
        Assert.That(HasAttribute(Method(controller, "RegisterAsync"), AllowAnonymous), Is.True, "Register must be anonymous");
        AssertRoute(Method(controller, "AuthenticateAsync"), HttpPost, "Authenticate");
        Assert.That(HasAttribute(Method(controller, "AuthenticateAsync"), AllowAnonymous), Is.True, "Authenticate must be anonymous");

        AssertRoute(Method(controller, "GetAllAsync"), HttpGet, null);
        Assert.That(Policy(Method(controller, "GetAllAsync")), Is.EqualTo("Service"), "user list is service-account only");

        AssertRoute(Method(controller, "GetByIdAsync"), HttpGet, "{id:long}");
        AssertRoute(Method(controller, "GetByUsernameAsync"), HttpGet, "username/{username}");
        AssertRoute(Method(controller, "DeleteAsync"), HttpDelete, "{id:long}");
        Assert.That(Policy(Method(controller, "DeleteAsync")), Is.EqualTo("Service"), "DELETE must be service-only");
    }

    [Test]
    public void NotificationsController_Route_And_Authorization_Contract()
    {
        var controller = Load("NotificationService.Api", "NotificationService.Api.Controllers.NotificationsController");
        AssertClassContract(controller);

        AssertRoute(Method(controller, "GetAllAsync"), HttpGet, null);
        AssertRoute(Method(controller, "GetByIdAsync"), HttpGet, "{id:long}");
        AssertRoute(Method(controller, "CreateAsync"), HttpPost, null);
        AssertRoute(Method(controller, "UpdateAsync"), HttpPut, "{id:long}");
        AssertRoute(Method(controller, "SendAsync"), HttpPost, "Send");
        AssertRoute(Method(controller, "DeleteAsync"), HttpDelete, "{id:long}");
        Assert.That(Policy(Method(controller, "DeleteAsync")), Is.EqualTo("Service"), "DELETE must be service-only");
    }

    [Test]
    public void NotificationHistoriesController_Route_And_Authorization_Contract()
    {
        var controller = Load("NotificationService.Api", "NotificationService.Api.Controllers.NotificationHistoriesController");
        AssertClassContract(controller);

        AssertRoute(Method(controller, "GetAllAsync"), HttpGet, null);
        AssertRoute(Method(controller, "GetByIdAsync"), HttpGet, "{key1:long}/{key2:long}");
        AssertRoute(Method(controller, "DeleteAsync"), HttpDelete, "{key1:long}/{key2:long}");
        Assert.That(Policy(Method(controller, "DeleteAsync")), Is.EqualTo("Service"), "DELETE must be service-only");
    }

    private static Type Load(string assemblyName, string typeName)
        => Assembly.Load(assemblyName).GetType(typeName, throwOnError: true)!;

    private static MethodInfo Method(Type controller, string name)
        => controller.GetMethod(name)
           ?? throw new AssertionException(controller.Name + "." + name + " not found -- route contract changed");

    private static bool HasAttribute(MemberInfo member, string attributeFullName)
        => member.GetCustomAttributesData().Any(a => a.AttributeType.FullName == attributeFullName);

    private static void AssertClassContract(Type controller)
    {
        Assert.That(HasAttribute(controller, ApiController), Is.True, controller.Name + " must carry [ApiController]");

        var routeAttr = controller.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == Route);
        var template = routeAttr is null ? null : routeAttr.ConstructorArguments.FirstOrDefault().Value as string;
        Assert.That(template, Is.EqualTo("api/[controller]"),
            controller.Name + " route template changed -- update ApiGateway/ocelot.json first");
    }

    private static void AssertRoute(MethodInfo method, string verbAttribute, string? expectedTemplate)
    {
        var attr = method.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == verbAttribute);
        Assert.That(attr, Is.Not.Null,
            method.DeclaringType!.Name + "." + method.Name + " must keep [" + verbAttribute + "]");

        var template = attr is not null && attr.ConstructorArguments.Count > 0
            ? attr.ConstructorArguments[0].Value as string
            : null;
        Assert.That(template, Is.EqualTo(expectedTemplate),
            method.DeclaringType!.Name + "." + method.Name + " route template changed -- update ApiGateway/ocelot.json first");
    }

    private static string? Policy(MethodInfo method)
    {
        var attr = method.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == Authorize);
        if (attr is null)
            return null;

        foreach (var named in attr.NamedArguments)
        {
            if (named.MemberName == "Policy")
                return named.TypedValue.Value as string;
        }

        return null;
    }
}