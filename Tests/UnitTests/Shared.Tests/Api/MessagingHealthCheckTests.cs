using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shared.Api;

using TestDoubles.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Api;

public class MessagingHealthCheckTests
{
    [Test]
    public async Task Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        await Assert.That(() => new MessagingHealthCheck(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CheckHealthAsync_WhenUseRabbitMqIsFalse_ReturnsHealthyInMemoryDescription()
    {
        var configuration = ConfigurationTestFactory.CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:UseRabbitMq"] = "false"
        });

        var healthCheck = new MessagingHealthCheck(configuration);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).Contains("In-memory");
    }

    [Test]
    public async Task CheckHealthAsync_WhenUnreachableBrokerConfigured_ReturnsUnhealthyStatus()
    {
        var configuration = ConfigurationTestFactory.CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:UseRabbitMq"] = "true",
            ["Messaging:RabbitMq:Host"] = "192.0.2.1",
            ["Messaging:RabbitMq:Port"] = "5672"
        });

        var healthCheck = new MessagingHealthCheck(configuration);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("unreachable");
    }

    [Test]
    public async Task CheckHealthAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        var configuration = ConfigurationTestFactory.CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:UseRabbitMq"] = "true",
            ["Messaging:RabbitMq:Host"] = "192.0.2.1"
        });

        var healthCheck = new MessagingHealthCheck(configuration);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(() => healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CheckHealthAsync_WhenProbeThrowsSocketException_ReturnsUnhealthy()
    {
        var configuration = ConfigurationTestFactory.CreateConfiguration(new Dictionary<string, string?>
        {
            ["Messaging:UseRabbitMq"] = "true",
            ["Messaging:RabbitMq:Host"] = "240.0.0.1",
            ["Messaging:RabbitMq:Port"] = "9999"
        });

        var healthCheck = new MessagingHealthCheck(configuration);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
    }
}
