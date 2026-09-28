using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UI.Models;
using UI.Services;

namespace UI.Tests;

public class StartupTests
{
    [Test]
    public async Task Startup_ConfigureServices_RegistersGatewayClientAndMvc()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var startup = new Startup(configuration);
        var services = new ServiceCollection();

        // Act
        startup.ConfigureServices(services);

        // Assert
        var gateway = services.SingleOrDefault(d => d.ServiceType == typeof(IGatewayApiClient));
        await Assert.That(gateway).IsNotNull();
        // Singleton: one RestClient/token cache shared across the app (the BFF shares one token cache process-wide)
        await Assert.That(gateway!.Lifetime).IsEqualTo(ServiceLifetime.Singleton);
        await Assert.That(gateway.ImplementationType).IsEqualTo(typeof(GatewayApiClient));

        // ApiSettings must be bound, otherwise the client throws at construction
        await Assert.That(services.Any(d => d.ServiceType == typeof(IConfigureOptions<ApiSettings>))).IsTrue();
        // AddControllersWithViews registers MvcOptions (RazorViewOptions lives in a package
        // UI.Tests does not reference, so it is not asserted here).
        await Assert.That(services.Any(d => d.ServiceType == typeof(IConfigureOptions<Microsoft.AspNetCore.Mvc.MvcOptions>))).IsTrue();
    }

    [Test]
    public async Task Program_CreateHostBuilder_InstantiatesValidBuilder()
    {
        // Act
        var builder = Program.CreateHostBuilder([]);

        // Assert
        await Assert.That(builder).IsNotNull();
        using var host = builder!.Build();
        await Assert.That(host).IsNotNull();
    }
}
