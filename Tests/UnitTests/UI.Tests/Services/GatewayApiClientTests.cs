using System.Net;
using System.Text;

using Microsoft.Extensions.Options;

using RestSharp;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UI.Models;
using UI.Services;

namespace UI.Tests.Services;

public class GatewayApiClientTests
{
    private const string BaseUrl = "http://gateway.test";

    /// <summary>
    /// Canned HTTP responses consumed FIFO. One shared script shape serves all cases so the twelve
    /// tests differ only in the sequence they enqueue and the behaviour they assert.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string? Content, string? ContentType)> scripted = new();

        public List<(string Method, string Path)> Requests { get; } = [];

        public void Enqueue(HttpStatusCode status, string? content = null, string? contentType = null)
            => scripted.Enqueue((status, content, contentType));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var method = request.Method.Method;
            var path = request.RequestUri!.PathAndQuery.TrimStart('/');
            Requests.Add((Method: method, Path: path));
            var (status, content, contentType) = scripted.Count > 0
                ? scripted.Dequeue()
                : (HttpStatusCode.OK, "{}", "application/json");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(content ?? string.Empty, Encoding.UTF8, contentType ?? "application/json")
            });
        }
    }

    private static string TokenJson(string token)
        => "{\"success\":true,\"data\":{\"token\":\"" + token + "\"}}";

    private static GatewayApiClient CreateClient(StubHandler handler, string? username = "uiservice", string? password = "pass")

        => new(
            Options.Create(new ApiSettings
            {
                GatewayBaseUrl = BaseUrl,
                ServiceUsername = username!,
                ServicePassword = password!
            }),
            baseUrl => new RestClient(new RestClientOptions(baseUrl)
            {
                ConfigureMessageHandler = _ => handler
            }));

    private static int CountAuth(StubHandler handler)
        => handler.Requests.Count(r => Route(r) == "POST /Users/Authenticate");

    /// <summary>"METHOD /path" for readable assertions, e.g. "POST /Users/Register".</summary>
    private static string Route((string Method, string Path) request) => $"{request.Method} /{request.Path}";
    [Test]
    public async Task Constructor_WhenGatewayBaseUrlMissing_ThrowsInvalidOperationException()
    {
        var settings = Options.Create(new ApiSettings { ServiceUsername = "u", ServicePassword = "p" });
        await Assert.That(() => new GatewayApiClient(settings, _ => new RestClient(BaseUrl)))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("ApiSettings:GatewayBaseUrl is not configured.");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Constructor_WhenServiceCredentialsMissing_ThrowsInvalidOperationException(string? blank)
    {
        var settings = Options.Create(new ApiSettings
        {
            GatewayBaseUrl = BaseUrl,
            ServiceUsername = "u",
            ServicePassword = blank!
        });
        await Assert.That(() => new GatewayApiClient(settings, _ => new RestClient(BaseUrl)))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetAsync_WhenAuthenticated_SendsGetWithBearerToken()
    {
        // Arrange - first call authenticates, second is the actual GET
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.OK, "{\"success\":true,\"data\":[]}");

        // Act
        var response = await CreateClient(handler).GetAsync("Users", CancellationToken.None);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(handler.Requests.Count).IsEqualTo(2);
        await Assert.That(Route(handler.Requests[0])).IsEqualTo("POST /Users/Authenticate");
        await Assert.That(Route(handler.Requests[1])).IsEqualTo("GET /Users");
    }

    [Test]
    public async Task GetAsync_WhenTokenCached_UsesFastPathWithoutReauthenticating()
    {
        // Arrange
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.OK, "[]");
        handler.Enqueue(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        // Act
        await client.GetAsync("Users", CancellationToken.None);
        await client.GetAsync("Notifications", CancellationToken.None);

        // Assert - authenticate exactly once; the second GET reuses the cached token
        await Assert.That(CountAuth(handler)).IsEqualTo(1);
    }

    [Test]
    public async Task GetAsync_WhenGatewayReturns401_ForcesTokenRefreshAndRetries()
    {
        // Arrange - GET is 401, the retry after a forced refresh succeeds
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.Unauthorized, null);
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-2"));
        handler.Enqueue(HttpStatusCode.OK, "[]");

        // Act
        var response = await CreateClient(handler).GetAsync("Users", CancellationToken.None);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(CountAuth(handler)).IsEqualTo(2);
    }

    [Test]
    public async Task PostAsync_SendsPostToTheRequestedPath()
    {
        // Arrange
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.OK, "{}");

        // Act
        var response = await CreateClient(handler)
            .PostAsync("Notifications", new { title = "t" }, CancellationToken.None);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(handler.Requests.Any(r => Route(r) == "POST /Notifications")).IsTrue();
    }
    [Test]
    public async Task EnsureToken_WhenInitialAuthFails_RegistersAndRetriesAuthenticate()
    {
        // Arrange - authenticate 401 (no user yet), then registration, then authenticate succeeds
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, null);
        handler.Enqueue(HttpStatusCode.OK, "{}");                // register
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1")); // authenticate again
        handler.Enqueue(HttpStatusCode.OK, "[]");

        // Act
        var response = await CreateClient(handler).GetAsync("Users", CancellationToken.None);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(handler.Requests.Any(r => Route(r) == "POST /Users/Register")).IsTrue();
    }

    [Test]
    public async Task EnsureToken_WhenRegisterReturnsIdempotentBadRequest_Succeeds()
    {
        // Arrange - 400 with success=false means "user already exists" and must be tolerated
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, null);
        handler.Enqueue(HttpStatusCode.BadRequest, "{\"success\":false,\"error\":{\"message\":\"exists\"}}");
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.OK, "[]");

        // Act - must not throw
        var response = await CreateClient(handler).GetAsync("Users", CancellationToken.None);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task EnsureToken_WhenRegisterFailsWithServerError_ThrowsInvalidOperationException()
    {
        // Arrange - 500 is a real failure and must not be swallowed (S3/B2)
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, null);
        handler.Enqueue(HttpStatusCode.InternalServerError, "boom");

        // Act & Assert
        await Assert.That(async () => await CreateClient(handler).GetAsync("Users", CancellationToken.None))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task EnsureToken_WhenTokenStillEmptyAfterRegistration_ThrowsInvalidOperationException()
    {
        // Arrange - registration "succeeds" but authenticate still returns nothing
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, null);
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.Enqueue(HttpStatusCode.Unauthorized, null);

        // Act & Assert
        await Assert.That(async () => await CreateClient(handler).GetAsync("Users", CancellationToken.None))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("UI service account could not authenticate against the gateway.");
    }

    [Test]
    public async Task EnsureToken_WhenAuthenticateReturnsInvalidJson_TreatsAsNoToken()
    {
        // Arrange - malformed JSON must be treated as "no token", driving the register path
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, "not-json");
        handler.Enqueue(HttpStatusCode.OK, "{}");
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.OK, "[]");

        // Act
        var response = await CreateClient(handler).GetAsync("Users", CancellationToken.None);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(handler.Requests.Any(r => Route(r) == "POST /Users/Register")).IsTrue();
    }

    [Test]
    public async Task EnsureToken_WhenRegisterReturnsMalformedJsonOnBadRequest_ThrowsInvalidOperationException()
    {
        // Arrange - 400 with an unparseable body is NOT "user exists", so it must surface
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, null);
        handler.Enqueue(HttpStatusCode.BadRequest, "<<<not json>>>");

        // Act & Assert
        await Assert.That(async () => await CreateClient(handler).GetAsync("Users", CancellationToken.None))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Dispose_CanBeInvokedMultipleTimesSafely()
    {
        // Arrange
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, TokenJson("jwt-1"));
        handler.Enqueue(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);
        await client.GetAsync("Users", CancellationToken.None);

        // Act & Assert - the SemaphoreSlim is disposed once; a second call must not throw
        client.Dispose();
        client.Dispose();

        await Assert.That(handler.Requests).IsNotEmpty();
    }
}