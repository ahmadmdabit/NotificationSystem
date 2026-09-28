using System.Reflection;

using TUnit.Assertions;
using TUnit.Assertions.Exceptions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

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
    public async Task Program_EntryPoints_Are_Public_For_WebApplicationFactory()
    {
        foreach (var assemblyName in new[] { "UserService.Api", "NotificationService.Api" })
        {
            var program = System.Reflection.Assembly.Load(assemblyName).GetType("Program");
            await Assert.That(program).IsNotNull()
                .Because(assemblyName + " must declare Program (public partial class Program) for WebApplicationFactory");
            await Assert.That(program!.IsPublic).IsTrue()
                .Because(assemblyName + ".Program must be public so tests can construct WebApplicationFactory<Program>");
        }
    }

    [Test]
    public async Task UsersController_Route_And_Authorization_Contract()
    {
        var controller = await Load("UserService.Api", "UserService.Api.Controllers.UsersController");
        await AssertClassContract(controller);

        await AssertRoute(Method(controller, "RegisterAsync"), HttpPost, "Register");
        await Assert.That(HasAttribute(Method(controller, "RegisterAsync"), AllowAnonymous)).IsTrue()
            .Because("Register must be anonymous");
        await AssertRoute(Method(controller, "AuthenticateAsync"), HttpPost, "Authenticate");
        await Assert.That(HasAttribute(Method(controller, "AuthenticateAsync"), AllowAnonymous)).IsTrue()
            .Because("Authenticate must be anonymous");

        await AssertRoute(Method(controller, "GetAllAsync"), HttpGet, null);
        await Assert.That(Policy(Method(controller, "GetAllAsync"))).IsEqualTo("Service")
            .Because("user list is service-account only");

        await AssertRoute(Method(controller, "GetByIdAsync"), HttpGet, "{id:long}");
        await AssertRoute(Method(controller, "GetByUsernameAsync"), HttpGet, "username/{username}");
        await AssertRoute(Method(controller, "DeleteAsync"), HttpDelete, "{id:long}");
        await Assert.That(Policy(Method(controller, "DeleteAsync"))).IsEqualTo("Service")
            .Because("DELETE must be service-only");
    }

    [Test]
    public async Task NotificationsController_Route_And_Authorization_Contract()
    {
        var controller = await Load("NotificationService.Api", "NotificationService.Api.Controllers.NotificationsController");
        await AssertClassContract(controller);

        await AssertRoute(Method(controller, "GetAllAsync"), HttpGet, null);
        await AssertRoute(Method(controller, "GetByIdAsync"), HttpGet, "{id:long}");
        await AssertRoute(Method(controller, "CreateAsync"), HttpPost, null);
        await AssertRoute(Method(controller, "UpdateAsync"), HttpPut, "{id:long}");
        await AssertRoute(Method(controller, "SendAsync"), HttpPost, "Send");
        await AssertRoute(Method(controller, "DeleteAsync"), HttpDelete, "{id:long}");
        await Assert.That(Policy(Method(controller, "DeleteAsync"))).IsEqualTo("Service")
            .Because("DELETE must be service-only");
    }

    [Test]
    public async Task NotificationHistoriesController_Route_And_Authorization_Contract()
    {
        var controller = await Load("NotificationService.Api", "NotificationService.Api.Controllers.NotificationHistoriesController");
        await AssertClassContract(controller);

        await AssertRoute(Method(controller, "GetAllAsync"), HttpGet, null);
        await AssertRoute(Method(controller, "GetByIdAsync"), HttpGet, "{key1:long}/{key2:long}");
        await AssertRoute(Method(controller, "DeleteAsync"), HttpDelete, "{key1:long}/{key2:long}");
        await Assert.That(Policy(Method(controller, "DeleteAsync"))).IsEqualTo("Service")
            .Because("DELETE must be service-only");
    }

    private static async Task<Type> Load(string assemblyName, string typeName)
        => System.Reflection.Assembly.Load(assemblyName).GetType(typeName, throwOnError: true)!;

    private static MethodInfo Method(Type controller, string name)
        => controller.GetMethod(name)
           ?? throw new AssertionException(controller.Name + "." + name + " not found -- route contract changed");

    private static bool HasAttribute(MemberInfo member, string attributeFullName)
        => member.GetCustomAttributesData().Any(a => a.AttributeType.FullName == attributeFullName);

    private static async Task AssertClassContract(Type controller)
    {
        await Assert.That(HasAttribute(controller, ApiController)).IsTrue()
            .Because(controller.Name + " must carry [ApiController]");

        var routeAttr = controller.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == Route);
        var template = routeAttr is null ? null : routeAttr.ConstructorArguments.FirstOrDefault().Value as string;
        await Assert.That(template).IsEqualTo("api/[controller]")
            .Because(controller.Name + " route template changed -- update ApiGateway/ocelot.json first");
    }

    private static async Task AssertRoute(MethodInfo method, string verbAttribute, string? expectedTemplate)
    {
        var attr = method.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == verbAttribute);
        await Assert.That(attr).IsNotNull()
            .Because(method.DeclaringType!.Name + "." + method.Name + " must keep [" + verbAttribute + "]");

        var template = attr is not null && attr.ConstructorArguments.Count > 0
            ? attr.ConstructorArguments[0].Value as string
            : null;
        await Assert.That(template).IsEqualTo(expectedTemplate)
            .Because(method.DeclaringType!.Name + "." + method.Name + " route template changed -- update ApiGateway/ocelot.json first");
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