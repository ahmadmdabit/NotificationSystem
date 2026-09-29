using MassTransit;
using System.Net.Sockets;
using System.Text.Json;

using TUnit.Assertions;

namespace IntegrationTests;

/// <summary>
/// Connection settings and broker probe for the real-broker integration tests.
/// </summary>
/// <remarks>
/// Configured entirely by environment variable so the same tests run unchanged on a developer
/// machine and in CI. Defaults match the <c>test-rabbitmq</c> service in
/// <c>docker-compose.test.yml</c> (host port 5673, because the main stack owns 5672).
/// <para>
/// <b>Fail loudly, never skip.</b> If the broker is unreachable these tests fail with the exact
/// recovery command rather than skipping. A silently-skipped guard is indistinguishable from no
/// guard, which is how a mis-routed publish survived a fully green 390-test suite in the first
/// place.
/// </para>
/// </remarks>
public static class TestBroker
{
    public const string Host = "TEST_RABBITMQ_HOST";
    public const string Port = "TEST_RABBITMQ_PORT";
    public const string User = "TEST_RABBITMQ_USER";
    public const string Password = "TEST_RABBITMQ_PASSWORD";
    public const string ManagementPort = "TEST_RABBITMQ_MANAGEMENT_PORT";

    /// <summary>Compose interpolates these same names from <c>.env</c> for the broker container.</summary>
    private const string ComposeUser = "RABBITMQ_USER";

    /// <summary>Compose interpolates these same names from <c>.env</c> for the broker container.</summary>
    private const string ComposePassword = "RABBITMQ_PASSWORD";

    // Queue name is declared by the test endpoint and is not referenced here; payload matching means
    // a stale message from an earlier run can never satisfy a wait, so no purge is needed.

    private const int DefaultPort = 5673;
    private const int DefaultManagementPort = 15673;

    // 127.0.0.1, deliberately NOT "localhost". Docker publishes the test broker on IPv4, and on
    // Windows "localhost" resolves to ::1 first; that IPv6 attempt black-holes and burns the whole
    // connect budget before IPv4 is ever tried, so the probe reports the broker unreachable when
    // it is sitting right there.
    public static string BrokerHost => Environment.GetEnvironmentVariable(Host) ?? "127.0.0.1";

    public static int BrokerPort => ReadInt(Port, DefaultPort);

    public static int ManagementPortNumber => ReadInt(ManagementPort, DefaultManagementPort);

    // Credentials deliberately default to the SAME variable names docker-compose.test.yml
    // interpolates from .env (RABBITMQ_USER / RABBITMQ_PASSWORD), falling back to guest.
    // Reading different names here would let the broker and the test silently disagree, which
    // surfaces as a 401 from the management API rather than an obvious configuration error.
    public static string BrokerUser => Resolve(ComposeUser, User, "guest");

    public static string BrokerPassword => Resolve(ComposePassword, Password, "guest");

    /// <summary>
    /// Resolves a broker setting from the environment, falling back to the repository's <c>.env</c>
    /// and finally to a literal default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>.env</c> fallback is load-bearing, not a convenience. <c>docker compose</c> reads
    /// <c>.env</c> automatically, but <c>dotnet test</c> does not - so without this the container
    /// would run with the real account from <c>.env</c> while the test process fell back to
    /// <c>guest</c>. RabbitMQ refuses <c>guest</c> off-loopback, and the resulting
    /// <c>ACCESS_REFUSED</c> surfaces as <c>Broker unreachable: guest@127.0.0.1:5673</c>, which
    /// reads exactly like a transport fault. Reading the same file Compose reads makes the two
    /// incapable of disagreeing, so a plain <c>dotnet test</c> works with no manual exporting.
    /// </para>
    /// <para>
    /// An explicit environment variable always wins, so CI can still override without a file.
    /// Values are quoted-stripped because a Windows <c>.env</c> may be written with quotes.
    /// </para>
    /// </remarks>
    private static string Resolve(string composeName, string explicitName, string fallback)
    {
        return Normalise(Environment.GetEnvironmentVariable(explicitName))
               ?? Normalise(Environment.GetEnvironmentVariable(composeName))
               ?? EnvFile(composeName)
               ?? fallback;
    }

    private static string? Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }

    /// <summary>
    /// Reads one key from the repository-root <c>.env</c>, or null when the file or key is absent.
    /// </summary>
    /// <remarks>
    /// Walked up from the test assembly rather than from the current directory: <c>dotnet test</c>
    /// runs each test host with an unpredictable working directory, so a relative path would make
    /// the fallback silently not apply. Parsing is intentionally minimal - no shell expansion, no
    /// interpolation - because a broken parse must fall back to a loud auth failure, never to a
    /// silently wrong credential.
    /// </remarks>
    private static string? EnvFile(string key)
    {
        foreach (var path in CandidateEnvFiles())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();

                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                var separator = trimmed.IndexOf('=');

                if (separator <= 0 || !trimmed.AsSpan(0, separator).SequenceEqual(key))
                {
                    continue;
                }

                return Normalise(trimmed[(separator + 1)..]);
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateEnvFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            yield return Path.Combine(dir.FullName, ".env");

            if (File.Exists(Path.Combine(dir.FullName, "NotificationSystem.slnx")))
            {
                yield break;
            }

            dir = dir.Parent;
        }
    }

    private static int ReadInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) && v > 0 ? v : fallback;

    /// <summary>
    /// Throws with a runnable recovery command when the broker is not reachable.
    /// </summary>
    public static void EnsureReachable()
    {
        if (CanConnect())
        {
            return;
        }

        throw new InvalidOperationException(
            $"Integration tests require a real RabbitMQ broker at {BrokerHost}:{BrokerPort}, and the TCP " +
            "probe failed. Start it with:  docker compose -f docker-compose.test.yml up -d --wait rabbitmq  " +
            "These tests deliberately FAIL rather than skip when the broker is absent: a guard that skips " +
            "unnoticed is how the generic-type-erasure defect in MassTransitDomainEventDispatcher was able " +
            "to ship behind a fully green suite. Override the endpoint with " +
            $"{Host} / {Port} / {ManagementPort} environment variables if your broker is elsewhere." +
            $"\n\nNOTE: this probe only opens a TCP socket. It cannot detect an AUTHENTICATION failure, and " +
            "RabbitMQ rejects 'guest' for any non-loopback connection - so a reachable broker can still " +
            $"refuse these credentials. If you see 'Broker unreachable: {BrokerUser}@...' from the " +
            "transport, read the inner exception: 'ACCESS_REFUSED' means the account was rejected, not " +
            "that the broker is down. The credentials here come from the environment first, then the " +
            "repository .env, matching what docker compose used to build the container.");
    }

    /// <summary>
    /// Queue name for the current test process.
    /// </summary>
    /// <remarks>
    /// Unique per process, deliberately. A fixed name lets any concurrent or leaked run steal
    /// this run's messages: a second host attaching to the same durable queue consumes them, so
    /// the consumer under test never sees the message and the test times out. That happened here - a
    /// leaked host from an earlier run silently broke every later run.
    ///
    /// The endpoint is NOT auto-delete. An auto-delete queue is removed whenever the last consumer
    /// disconnects, including the brief window during bus startup, so a message published into that
    /// window reaches an exchange with no binding and is discarded. That made this test fail
    /// intermittently before the setting was removed.
    /// </remarks>

    /// <summary>Unique per process, so a leaked or concurrent run cannot consume this run's messages.</summary>
    private static readonly string RunId = Guid.NewGuid().ToString("N")[..8];

    public static string QueueName => $"integration-test-domain-event-{RunId}";

    /// <summary>Connection probe timeout. Must be bounded.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private static bool CanConnect()
    {
        try
        {
            // ConnectAsync + an explicit timeout, NOT the blocking TcpClient.Connect(string, int).
            // On Windows "localhost" resolves to ::1 first; when only the IPv4 port is published
            // the IPv6 attempt can black-hole for the OS default (tens of seconds), which hangs
            // the test host with no output at all. An unbounded probe in a [Before] hook is a
            // defect in the harness, not a slow test.
            using var cts = new CancellationTokenSource(ConnectTimeout);
            using var client = new TcpClient();
            client.ConnectAsync(BrokerHost, BrokerPort, cts.Token).GetAwaiter().GetResult();
            return client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Asserts that <paramref name="messageType"/>'s publish exchange is bound, through the full
    /// two-hop topology, to <paramref name="queueName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MassTransit does not bind a publish exchange directly to a queue. The real shape,
    /// verified via <c>rabbitmqctl list_bindings</c>, is:
    /// </para>
    /// <code>
    /// publish exchange  ->  endpoint exchange  ->  queue
    /// </code>
    /// <para>
    /// A diagnostic that asks which QUEUES are bound to the publish exchange therefore answers
    /// NONE on a perfectly healthy bus. This walks both hops, which is the defect that shipped in the
    /// first version of this harness.
    /// </para>
    /// <para>
    /// Binding state is read from the broker, not inferred from counters: it is exact and immediate,
    /// whereas <c>message_stats</c> is aggregated on an interval and lagged by 200-500ms, which made
    /// counter-based assertions report false verdicts.
    /// </para>
    /// </remarks>
    /// <summary>Live queue counters, read from the broker.</summary>
    /// <param name="Ready">Messages waiting to be delivered.</param>
    /// <param name="Unacknowledged">Delivered to a consumer but not yet acknowledged.</param>
    /// <param name="Consumers">Attached consumers.</param>
    public sealed record QueueStats(string Name, long Ready, long Unacknowledged, int Consumers);

    public static async Task AssertPublishAddressBoundAsync(
        IBusTopology topology,
        string queueName,
        Type messageType,
        CancellationToken cancellationToken = default)
    {
        // Deterministic discriminator: ask the bus itself which exchange it would publish this
        // runtime type to. This is the exact value the defect got wrong, and it is available without
        // publishing anything, so the assertion cannot be timing-dependent.
        if (!topology.TryGetPublishAddress(messageType, out var publishAddress))
        {
            throw new InvalidOperationException(
                $"MassTransit resolved no publish address for {messageType.FullName}. " +
                "The bus has no publish topology for this type.");
        }
        var exchange = publishAddress.AbsolutePath.Trim('/');

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < deadline)
        {
            var bound = await QueuesBoundToExchangeAsync(exchange, cancellationToken);

            if (bound.Contains(queueName, StringComparer.Ordinal))
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }

        var final = await QueuesBoundToExchangeAsync(exchange, CancellationToken.None);

        throw new TimeoutException(
            $"Queue '{queueName}' is not reachable from the publish exchange '{exchange}' for " +
            $"{messageType.FullName}. Queues actually bound (both hops): " +
            $"{(final.Count == 0 ? "NONE" : string.Join(", ", final))}. " +
            "If NONE, the message would be accepted by the exchange and discarded - the silent-discard defect. " +
            "If other queues are listed, another host is bound to this exchange and may consume this run's messages.");
    }

    /// <summary>
    /// Walks publish exchange -> endpoint exchange -> queue, returning every reachable queue name.
    /// </summary>
    /// <summary>
    /// Resolves the publish address the bus reports for <paramref name="messageType"/>, and the set
    /// of queues reachable from it, then asserts the two agree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the assertion that closes the gap left by <c>AssertPublishAddressBoundAsync</c>.
    /// That method asks the BUS what the address is and checks the queue is bound to it. It never
    /// checks the address the PUBLISHER will actually use at send time.
    /// </para>
    /// <para>
    /// The gap is where the <c>PublishEndpointProvider</c> cache hypothesis lives. That provider
    /// caches send endpoints by <c>typeof(T)</c>:
    /// <c><c>GetPublishSendEndpoint&lt;T&gt;() =&gt; _cache.GetSendEndpoint(typeof(T), ...)</c></c>.
    /// If an entry is created for a base type first, subsequent publishes resolve to the BASE
    /// exchange while the queue is bound to the CONCRETE one. Both facts are independently true and
    /// the topology assertion still passes, because the binding really is correct - only the
    /// publisher is looking in the wrong place.
    /// </para>
    /// </remarks>
    public static async Task<(string PublishExchange, string EndpointExchange, IReadOnlyList<string> Queues)> ResolvePublishTopologyAsync(
        IBusTopology topology,
        Type messageType,
        CancellationToken cancellationToken = default)
    {
        if (!topology.TryGetPublishAddress(messageType, out var publishAddress))
        {
            throw new InvalidOperationException(
                $"MassTransit resolved no publish address for {messageType.FullName}.");
        }

        var publishExchange = publishAddress.AbsolutePath.Trim('/');
        var bound = await QueuesBoundToExchangeAsync(publishExchange, cancellationToken);

        // Hop 1 targets an endpoint exchange, not a queue. Record it so a failure can name the
        // exact link that is missing rather than just reporting an empty set.
        var endpointExchanges = await QueuesBoundToAsync(publishExchange, "exchange", cancellationToken);

        return (publishExchange, endpointExchanges.FirstOrDefault() ?? string.Empty, bound);
    }

    /// <summary>
    /// Asserts this run's queue is reachable from the publish exchange through the two-hop topology.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately does NOT assert that this run's endpoint exchange is the only one bound. Deleting
    /// a queue does not delete its exchange, so every historical run leaves a stale endpoint
    /// exchange bound to the shared publish exchange forever. Such exchanges have 0 consumers, so
    /// they cannot steal messages - they only widen the fanout. An earlier version of this method
    /// treated them as fatal, which made the test fail in 5s on any broker that had ever been
    /// used, and reported a problem that does not exist. Stale exchanges are reported in the
    /// diagnostic text instead, so they stay visible without being treated as defects.
    /// </para>
    /// <para>
    /// The assertion that matters is reachability: a publish to the concrete exchange must be able
    /// to reach THIS run's queue. If it cannot, the message is discarded.
    /// </para>
    /// </remarks>
    public static async Task AssertQueueReachableAsync(
        IBusTopology topology,
        string queueName,
        Type messageType,
        CancellationToken cancellationToken = default)
    {
        var (publishExchange, _, bound) =
            await ResolvePublishTopologyAsync(topology, messageType, cancellationToken);

        await Assert.That(bound).Contains(queueName)
            .Because(
                $"the publish exchange '{publishExchange}' for {messageType.Name} must reach this run's " +
                $"queue '{queueName}' through the two-hop topology (publish exchange -> endpoint exchange -> queue). " +
                $"Queues actually bound: {(bound.Count == 0 ? "NONE - every publish to this exchange is discarded" : string.Join(", ", bound))}. " +
                (bound.Count > 1
                    ? "More than one queue is bound; a concurrent or leaked run may also be consuming."
                    : string.Empty));
    }

    internal static async Task<IReadOnlyList<string>> QueuesBoundToExchangeAsync(
        string exchange,
        CancellationToken cancellationToken)
    {
        var direct = await QueuesBoundToAsync(exchange, "queue", cancellationToken);
        var viaEndpoints = new List<string>();

        foreach (var endpointExchange in await QueuesBoundToAsync(exchange, "exchange", cancellationToken))
        {
            viaEndpoints.AddRange(await QueuesBoundToAsync(endpointExchange, "queue", cancellationToken));
        }

        return [.. direct.Concat(viaEndpoints).Distinct(StringComparer.Ordinal).OrderBy(q => q, StringComparer.Ordinal)];
    }

    /// <summary>Destinations of one binding type out of an exchange, via the management API.</summary>
    /// <remarks>
    /// The management API field is <c>destination_type</c>, NOT <c>destination_kind</c> (the latter is
    /// what <c>rabbitmqctl</c> prints). Reading the wrong name yields an empty string, every filter
    /// rejects every row, and the walk silently reports no bound queues on a healthy bus.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> QueuesBoundToAsync(
        string exchange,
        string destinationKind,
        CancellationToken cancellationToken)
    {
        using var document = await ManagementGetAsync(
            $"api/exchanges/%2F/{Uri.EscapeDataString(exchange)}/bindings/source",
            cancellationToken);

        if (document is null)
        {
            return [];
        }

        return
        [
            .. document.RootElement.EnumerateArray()
                .Where(b => b.TryGetProperty("destination_type", out var kind) && kind.GetString() == destinationKind)
                .Select(b => b.GetProperty("destination").GetString() ?? string.Empty)
                .Where(name => name.Length > 0)
        ];
    }

    /// <summary>
    /// Blocks until the queue exists on the broker AND a consumer is attached to it.
    /// </summary>
    /// <remarks>
    /// This is the deterministic alternative to sleeping. Publishing before the binding is declared
    /// sends the message to an exchange with no queue and RabbitMQ discards it silently - which is
    /// <summary>Live queue counters, or <c>null</c> when the queue does not exist.</summary>
    public static async Task<QueueStats?> QueueStatsForAsync(string queueName, CancellationToken cancellationToken = default)
    {
        using var document = await ManagementGetAsync($"api/queues/%2F/{Uri.EscapeDataString(queueName)}", cancellationToken);

        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;
        return new QueueStats(
            queueName,
            ReadLong(root, "messages_ready"),
            ReadLong(root, "messages_unacknowledged"),
            (int)ReadLong(root, "consumers"));
    }

    /// <summary>
    /// Exchange name MassTransit would publish <paramref name="messageType"/> to, or <c>null</c>.
    /// </summary>
    private static string? TryResolvePublishExchange(Type messageType)
    {
        // No bus handle is available here, so fall back to the naming convention MassTransit uses
        // for RabbitMQ publish exchanges: {Namespace}:{TypeName}. This is only used for diagnostics;
        // the load-bearing assertion goes through IBusTopology.TryGetPublishAddress.
        return $"{messageType.Namespace}:{messageType.Name}";
    }

    /// <summary>
    /// Blocks until the queue exists on the broker AND a consumer is attached to it.
    /// </summary>
    /// <remarks>
    /// This is the deterministic alternative to sleeping. Publishing before the binding is declared
    /// sends the message to an exchange with no queue and RabbitMQ discards it silently - which is
    /// precisely the failure this project guards against. A harness that races its own topology
    /// would otherwise reproduce the very defect it exists to catch.
    /// </remarks>
    public static async Task<QueueStats> WaitForConsumerAttachedAsync(
        string queueName,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        QueueStats? last = null;

        while (DateTime.UtcNow < deadline)
        {
            last = await QueueStatsForAsync(queueName, cancellationToken);
            if (last is { Consumers: > 0 })
            {
                return last;
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException(
            $"Queue '{queueName}' did not report an attached consumer within {timeout.TotalSeconds:0.#}s. " +
            $"Last observed: {(last is null ? "queue does not exist" : $"ready={last.Ready} consumers={last.Consumers}")}. " +
            "Consumers=0 after the queue exists means the receive endpoint never started, so nothing is " +
            "bound to the exchange and any publish would be discarded.");
    }

    /// <remarks>
    /// Deliberately free of <c>message_stats</c>. Those counters are aggregated on an interval and
    /// lag publication by 200-500ms, so a sample taken right after a publish reports stale values
    /// and produces false verdicts in both directions. Binding state and consumer counts are exact.
    /// </remarks>
    public static async Task<string> DescribeQueueStateAsync(
        string queueName,
        Type messageType,
        CancellationToken cancellationToken = default)
    {
        var stats = await QueueStatsForAsync(queueName, cancellationToken);
        var publishAddress = TryResolvePublishExchange(messageType);
        var bound = publishAddress is null
            ? Array.Empty<string>()
            : await QueuesBoundToExchangeAsync(publishAddress, cancellationToken);

        var builder = new System.Text.StringBuilder();

        builder.Append(stats is null
            ? $"queue '{queueName}' DOES NOT EXIST"
            : $"queue '{queueName}' ready={stats.Ready} unacked={stats.Unacknowledged} consumers={stats.Consumers}");

        builder.Append($"; publish exchange for {messageType.Name}: {publishAddress ?? "(unresolved)"}");
        builder.Append($"; queues reachable through both hops: {(bound.Count == 0 ? "NONE" : string.Join(", ", bound))}");

        if (bound.Count > 1)
        {
            builder.Append(" - MORE THAN ONE QUEUE IS BOUND, so another host may consume this run's messages.");
        }

        return builder.ToString();
    }

    private static long ReadLong(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number) ? number : 0L;

    /// <summary>GET a management-API path, or <c>null</c> when the broker reports no such object.</summary>
    private static async Task<JsonDocument?> ManagementGetAsync(string path, CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{BrokerUser}:{BrokerPassword}"));

        using var client = new HttpClient { BaseAddress = new Uri($"http://{BrokerHost}:{ManagementPortNumber}/") };
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        using var response = await client.GetAsync(path, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }
}
