using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Application;
using NotificationService.Infrastructure;
using NUnit.Framework;
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
        Assert.That(dispatcher, Is.Not.Null, "UserService IDomainEventDispatcher must be registered (W-3)");
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
        Assert.That(uow, Is.Not.Null);
        Assert.That(uow!.Transaction, Is.Null, "No transaction before Begin");
        }
    }

    [Test]
    public async Task UserService_UserRepository_Resolves()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
        Assert.That(scope.ServiceProvider.GetService<UserService.Domain.Abstractions.IUserRepository>(), Is.Not.Null);
        }
    }

    [Test]
    public async Task UserService_TokenService_Resolves()
    {
        var (provider, scope) = BuildUserScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
        Assert.That(scope.ServiceProvider.GetService<UserService.Domain.Abstractions.ITokenService>(), Is.Not.Null);
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

        Assert.That(plain, Is.Not.Empty);
        Assert.That(service, Is.Not.Empty);
        Assert.That(service, Is.Not.EqualTo(plain), "Role claim must alter the token");
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
        Assert.That(dispatcher, Is.Not.Null);
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
        Assert.That(uow, Is.Not.Null);
        Assert.That(uow!.Transaction, Is.Null, "No transaction before Begin");
        }
    }

    [Test]
    public async Task NotificationService_Repositories_Resolve()
    {
        var (provider, scope) = BuildNotificationScope();
        await using (scope)
        await using (provider.ConfigureAwait(false))
        {
        Assert.That(scope.ServiceProvider.GetService<NotificationService.Domain.Abstractions.INotificationRepository>(), Is.Not.Null);
        Assert.That(scope.ServiceProvider.GetService<NotificationService.Domain.Abstractions.INotificationHistoryRepository>(), Is.Not.Null);
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
            Assert.That(result, Is.EqualTo("pong"),
                "MediatR must resolve the handler through the Validation/Logging/Transaction behavior chain");
        }
    }

    [Test]
    public void Dapper_DateTime_Maps_To_DateTime2_And_Configure_Is_Idempotent()
    {
        // One-shot guard must hold under concurrent calls (Interlocked.Exchange).
        Parallel.For(0, 8, _ => Shared.Infrastructure.DapperConfiguration.Configure());
        Shared.Infrastructure.DapperConfiguration.Configure();

        // The real contract: DateTime/DateTime? must be remapped to DbType.DateTime2 in
        // Dapper's type map (pinned Dapper 2.1.x internals -- see PackageReference).
        var field = typeof(Dapper.SqlMapper).GetField(
            "typeMap",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.That(field, Is.Not.Null,
            "Dapper SqlMapper.typeMap not found -- Dapper drifted beyond the pinned 2.1.x line");

        var map = (System.Collections.IDictionary)field!.GetValue(null)!;
        AssertMappedToDateTime2(map, typeof(DateTime));
        AssertMappedToDateTime2(map, typeof(DateTime?));
    }

    private static void AssertMappedToDateTime2(System.Collections.IDictionary map, Type type)
    {
        Assert.That(map.Contains(type), Is.True,
            type.Name + " missing from Dapper type map -- DapperConfiguration.Configure() did not run");

        var entry = map[type]!;
        var dbType = entry.GetType().GetProperty("DbType")?.GetValue(entry)
                     ?? entry.GetType().GetField("DbType")?.GetValue(entry);
        Assert.That(dbType, Is.EqualTo(System.Data.DbType.DateTime2),
            type.Name + " must map to DbType.DateTime2 (datetime2 mapping rule)");
    }
}

/// <summary>Test-only request proving the MediatR pipeline end-to-end without SQL.</summary>
public sealed record PipelineRoundTripQuery : IRequest<string>;

public sealed class PipelineRoundTripHandler : IRequestHandler<PipelineRoundTripQuery, string>
{
    public Task<string> Handle(PipelineRoundTripQuery request, CancellationToken cancellationToken)
        => Task.FromResult("pong");
}
