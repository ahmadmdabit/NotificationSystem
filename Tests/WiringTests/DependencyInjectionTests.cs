using System.Globalization;
using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NotificationService.Application;
using NotificationService.Infrastructure;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application;
using UserService.Infrastructure;

namespace WiringTests;

/// <summary>
/// DI wiring smoke tests — verify every registered service resolves.
/// These would have caught the W-3 (missing IDomainEventDispatcher) class of defect.
/// MassTransit registrations only implement IAsyncDisposable, so providers are disposed async.
/// </summary>
public class DependencyInjectionTests
{
    private static (ServiceProvider Provider, AsyncServiceScope Scope) BuildUserScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:Secret"] = new string('a', 64),
                ["AppSettings:SqlConnectionString"] = "Server=localhost;Database=UserDB;User Id=sa;Password=x;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUserServiceApplication();
        services.AddUserServiceInfrastructure(configuration);
        // Test-only request handler so the MediatR round trip exercises the real
        // Validation/Logging/Transaction pipeline without touching SQL.
        services.AddTransient<IRequestHandler<PipelineRoundTripQuery, string>, PipelineRoundTripHandler>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true, // any unconstructable registration fails the suite
            ValidateScopes = true   // captive-dependency violations surface at resolve time
        });
        return (provider, provider.CreateAsyncScope());
    }

    private static (ServiceProvider Provider, AsyncServiceScope Scope) BuildNotificationScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:Secret"] = new string('a', 64),
                ["AppSettings:SqlConnectionString"] = "Server=localhost;Database=NotificationDB;User Id=sa;Password=x;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationServiceApplication();
        services.AddNotificationServiceInfrastructure(configuration);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        return (provider, provider.CreateAsyncScope());
    }

    [Test]
    public async Task UserService_DomainEventDispatcher_Resolves()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            var dispatcher = scope.ServiceProvider.GetService<Shared.Domain.Abstractions.IDomainEventDispatcher>();
            await Assert.That(dispatcher).IsNotNull()
                .Because("UserService IDomainEventDispatcher must be registered (W-3)");
        }
    }

    [Test]
    public async Task UserService_UnitOfWork_Resolves_And_ExposesTransaction()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            var uow = scope.ServiceProvider.GetService<Shared.Application.Abstractions.IUnitOfWork>();
            await Assert.That(uow).IsNotNull();
            await Assert.That(uow!.Transaction).IsNull()
                .Because("No transaction before Begin");
        }
    }

    [Test]
    public async Task UserService_UserRepository_Resolves()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            await Assert.That(scope.ServiceProvider.GetService<UserService.Domain.Abstractions.IUserRepository>()).IsNotNull();
        }
    }

    [Test]
    public async Task UserService_TokenService_Resolves()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            await Assert.That(scope.ServiceProvider.GetService<UserService.Domain.Abstractions.ITokenService>()).IsNotNull();
        }
    }

    [Test]
    public async Task UserService_TokenService_MintsToken_WithAndWithoutRole()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<UserService.Domain.Abstractions.ITokenService>();

            var plain = tokenService.GenerateToken(42);
            var service = tokenService.GenerateToken(42, "service");

            await Assert.That(plain).IsNotEmpty();
            await Assert.That(service).IsNotEmpty();
            await Assert.That(service).IsNotEqualTo(plain)
                .Because("Role claim must alter the token");
        }
    }

    [Test]
    public async Task NotificationService_DomainEventDispatcher_Resolves()
    {
        var (provider, scope) = BuildNotificationScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            var dispatcher = scope.ServiceProvider.GetService<Shared.Domain.Abstractions.IDomainEventDispatcher>();
            await Assert.That(dispatcher).IsNotNull();
        }
    }

    [Test]
    public async Task NotificationService_UnitOfWork_Resolves_And_ExposesTransaction()
    {
        var (provider, scope) = BuildNotificationScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            var uow = scope.ServiceProvider.GetService<Shared.Application.Abstractions.IUnitOfWork>();
            await Assert.That(uow).IsNotNull();
            await Assert.That(uow!.Transaction).IsNull()
                .Because("No transaction before Begin");
        }
    }

    [Test]
    public async Task NotificationService_Repositories_Resolve()
    {
        var (provider, scope) = BuildNotificationScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            await Assert.That(scope.ServiceProvider.GetService<NotificationService.Domain.Abstractions.INotificationRepository>()).IsNotNull();
            await Assert.That(scope.ServiceProvider.GetService<NotificationService.Domain.Abstractions.INotificationHistoryRepository>()).IsNotNull();
        }
    }

    [Test]
    public async Task Mediator_RoundTrip_Executes_Validation_Logging_And_Transaction_Pipelines()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new PipelineRoundTripQuery());
            await Assert.That(result).IsEqualTo("pong")
                .Because("MediatR must resolve the handler through the Validation/Logging/Transaction behavior chain");
        }
    }

    [Test]
    [NotInParallel]
    public async Task Dapper_DateTime_Maps_To_DateTime2_And_Configure_Is_Idempotent()
    {
        // One-shot guard must hold under concurrent calls (Interlocked.Exchange).
        Parallel.For(0, 8, _ => Shared.Infrastructure.DapperConfiguration.Configure());
        Shared.Infrastructure.DapperConfiguration.Configure();

        // The real contract: DateTime/DateTime? must be remapped to DbType.DateTime2 in
        // Dapper's type map (pinned Dapper 2.1.x internals -- see PackageReference).
        var field = typeof(Dapper.SqlMapper).GetField(
            "typeMap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        await Assert.That(field).IsNotNull()
            .Because("Dapper SqlMapper.typeMap not found -- Dapper drifted beyond the pinned 2.1.x line");

        var map = (System.Collections.IDictionary)field!.GetValue(null)!;
        await AssertMappedToDateTime2(map, typeof(DateTime));
        await AssertMappedToDateTime2(map, typeof(DateTime?));
    }

    /// <summary>
    /// Mirrors the <c>Program.cs</c> registration block of one service API so the
    /// graph that ships is covered by a test (F-16 / R-16).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Graph validation is deliberately OFF for this scope.</b> Two reasons, both learned the
    /// hard way:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <c>ValidateOnBuild</c> does <i>not</i> catch the defect this test exists for.
    /// <c>AddExceptionHandler&lt;T&gt;()</c> registers a lazily-constructed singleton: validation
    /// inspects the descriptor but never calls the constructor, so F-01 stayed green. The guard
    /// therefore <i>actively resolves</i> <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandler"/>
    /// — see R-23. Removing the eager resolve re-opens the blind spot.
    /// </item>
    /// <item>
    /// It also reports false positives here. MVC's own descriptors
    /// (<c>ControllerActionInvokerProvider</c>, <c>ControllerRequestDelegateFactory</c>, …) depend
    /// on services that <c>WebApplicationBuilder</c> supplies but a bare
    /// <see cref="ServiceCollection"/> does not, so validation fails on framework plumbing that
    /// works correctly in production — noise that would mask a real signal.
    /// </item>
    /// </list>
    /// <para>
    /// <c>ValidateScopes</c> stays on: captive-dependency detection produces no such false
    /// positives and is a real defect class.
    /// </para>
    /// <para>
    /// <c>DatabaseMigration</c> is deliberately NOT registered as a hosted service: it opens a
    /// live SQL connection on start. It is resolved by nothing in this graph, so omitting it
    /// keeps the test database-free.
    /// </para>
    /// <para>
    /// Swagger, JWT auth, CORS and authorization policies are out of scope and intentionally not
    /// mirrored — they need a config secret or an extra package reference. The guard's purpose is
    /// constructor resolvability of the exception/health pipeline.
    /// </para>
    /// </remarks>
    private static (ServiceProvider Provider, AsyncServiceScope Scope) BuildApiScope(bool notificationService)
    {
        var database = notificationService ? "NotificationDB" : "UserDB";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:Secret"] = new string('a', 64),
                ["AppSettings:SqlConnectionString"] =
                    $"Server=localhost;Database={database};User Id=sa;Password=x;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        if (notificationService)
        {
            services.AddNotificationServiceApplication();
            services.AddNotificationServiceInfrastructure(configuration);
        }
        else
        {
            services.AddUserServiceApplication();
            services.AddUserServiceInfrastructure(configuration);
        }

        // IHostEnvironment / IWebHostEnvironment are supplied by the host builder in production;
        // MVC's own registrations consume IWebHostEnvironment. Registering them here is what lets
        // this guard detect a ctor that asks for a primitive instead of the interface — F-01's
        // `string environmentName` is resolvable by nothing, and throws on blank input.
        var environment = new StubWebHostEnvironment { EnvironmentName = "Production" };
        services.AddSingleton(environment);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(environment);
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(environment);

        services.AddControllers()
            .ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);
        services.AddExceptionHandler<Shared.Api.ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddHealthChecks().AddCheck<Shared.Api.MessagingHealthCheck>("messaging");

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            // See the remarks above: graph validation both misses F-01 and false-positives on MVC.
            ValidateOnBuild = false,
            ValidateScopes = true
        });
        return (provider, provider.CreateAsyncScope());
    }

    [Test]
    public async Task UserService_ApiRegistrations_AllResolve()
    {
        var (provider, scope) = BuildApiScope(notificationService: false);
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            await AssertExceptionHandlerResolves(scope.ServiceProvider);
            await AssertMessagingHealthCheckRegistered(scope.ServiceProvider);
        }
    }

    [Test]
    public async Task NotificationService_ApiRegistrations_AllResolve()
    {
        var (provider, scope) = BuildApiScope(notificationService: true);
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
            await AssertExceptionHandlerResolves(scope.ServiceProvider);
            await AssertMessagingHealthCheckRegistered(scope.ServiceProvider);
        }
    }

    private static async Task AssertExceptionHandlerResolves(IServiceProvider services)
    {
        var handler = services.GetRequiredService<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();
        await Assert.That(handler).IsNotNull()
            .Because("ApiExceptionHandler must be DI-constructible — it is the only place the ApiResult envelope is produced on the error path (F-01)");
        await Assert.That(handler).IsTypeOf<Shared.Api.ApiExceptionHandler>();
    }

    /// <summary>
    /// <c>AddCheck&lt;T&gt;(name)</c> registers an <i>instance</i>, so <c>MessagingHealthCheck</c> is
    /// not resolvable as a service. The registration is asserted through the options bag instead,
    /// which is also what proves the name and the implementation both survived.
    /// </summary>
    private static async Task AssertMessagingHealthCheckRegistered(IServiceProvider services)
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>();

        var registration = options.Value.Registrations
            .FirstOrDefault(r => r.Name == "messaging");

        await Assert.That(registration).IsNotNull()
            .Because("Program.cs registers MessagingHealthCheck under the name \"messaging\"");
        await Assert.That(registration!.Factory).IsNotNull();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Messaging_Disabled_ResolvesADispatcherAndRegistersNoBus(bool notificationService)
    {
        // R-22: MassTransit 9.x refuses to create a bus without a commercial licence, and the
        // gate runs before transport selection - so the in-memory path is not licence-free.
        // Messaging:Enabled=false must leave the container fully resolvable, with a null-object
        // dispatcher standing in for MassTransitDomainEventDispatcher.
        //
        // N-11: this now runs for BOTH services. NotificationService has an identical if/else
        // branch in its DependencyInjection, and a regression that deleted it (or registered
        // both dispatchers) would previously have stayed green because only UserService was
        // covered. Mutation-verify: delete the else branch in
        // NotificationService.Infrastructure/DependencyInjection.cs and this must go red.
        var database = notificationService ? "NotificationDB" : "UserDB";
        var which = notificationService ? "NotificationService" : "UserService";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:Secret"] = new string('a', 64),
                ["AppSettings:SqlConnectionString"] =
                    $"Server=localhost;Database={database};User Id=sa;Password=x;TrustServerCertificate=True",
                ["Messaging:Enabled"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        if (notificationService)
        {
            services.AddNotificationServiceApplication();
            services.AddNotificationServiceInfrastructure(configuration);
        }
        else
        {
            services.AddUserServiceApplication();
            services.AddUserServiceInfrastructure(configuration);
        }

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        await using (provider.ConfigureAwait(false))
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Shared.Domain.Abstractions.IDomainEventDispatcher>();
            await Assert.That(dispatcher).IsTypeOf<Shared.Infrastructure.NullDomainEventDispatcher>()
                .Because($"{which}: with no bus registered, the post-commit dispatch path must fall back to the null object rather than fail to resolve");
            await Assert.That(scope.ServiceProvider.GetService<MassTransit.IPublishEndpoint>()).IsNull()
                .Because($"{which}: Messaging:Enabled=false must not register MassTransit at all - that is the whole point of the switch");
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments("true")]
    [Arguments("")]
    public async Task Messaging_Enabled_IsTheDefault(string? configured)
    {
        // An absent, malformed or blank key must leave messaging ON. Defaulting the other way
        // would silently disable the broker in a deployment that simply forgot the setting.
        var values = new Dictionary<string, string?>();
        if (configured is not null)
            values["Messaging:Enabled"] = configured;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        await Assert.That(UserService.Infrastructure.Messaging.MassTransitConfigurator.IsMessagingEnabled(configuration))
            .IsTrue()
            .Because($"Messaging:Enabled={configured ?? "<absent>"} must be treated as enabled");
    }

    [Test]
    public async Task Messaging_EnabledFalse_IsRespected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Messaging:Enabled"] = "false" })
            .Build();

        await Assert.That(UserService.Infrastructure.Messaging.MassTransitConfigurator.IsMessagingEnabled(configuration))
            .IsFalse();
    }

    private static async Task AssertMappedToDateTime2(System.Collections.IDictionary map, Type type)
    {
        await Assert.That(map.Contains(type)).IsTrue()
    .Because(type.Name + " missing from Dapper type map -- DapperConfiguration.Configure() did not run");

        var entry = map[type]!;
        var dbType = entry.GetType().GetProperty("DbType")?.GetValue(entry)
                     ?? entry.GetType().GetField("DbType")?.GetValue(entry);
        // Compare the numeric value, not the boxed type: Assert.That(object) is
        // type-strict, so an int-typed box can never equal a DbType even though
        // DbType.DateTime2 IS -2. dbType is a DbType or an integral representation
        // of one, and this is the only value the test actually cares about.
        await Assert.That(Convert.ToInt32(dbType, CultureInfo.InvariantCulture))
            .IsEqualTo((int)System.Data.DbType.DateTime2)
            .Because(type.Name + " must map to DbType.DateTime2 (datetime2 mapping rule)");
    }
}

/// <summary>Test-only request proving the MediatR pipeline end-to-end without SQL.</summary>
public sealed record PipelineRoundTripQuery : IRequest<string>;

public sealed class PipelineRoundTripHandler : IRequestHandler<PipelineRoundTripQuery, string>
{
    public Task<string> Handle(PipelineRoundTripQuery request, CancellationToken cancellationToken)
        => Task.FromResult("pong");
}

/// <summary>
/// Minimal <see cref="Microsoft.AspNetCore.Hosting.IWebHostEnvironment"/> (which derives from
/// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment"/>) for the R-16 guard.
/// Deliberately local to this project: <c>TestDoubles</c> is the shared-doubles library for the
/// four unit-test projects, and wiring them together would couple the projects the plan keeps
/// independent. The type is intentionally trivial, so the duplication carries no drift risk.
/// </summary>
internal sealed class StubWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "WiringTests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
        = new Microsoft.Extensions.FileProviders.NullFileProvider();
    // Deliberately non-nullable: IWebHostEnvironment.WebRootPath is declared string, and an
    // explicit Nullability annotation here is what produces CS8766.
    public string WebRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
        = new Microsoft.Extensions.FileProviders.NullFileProvider();
}
