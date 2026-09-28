using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

using RestSharp;

using Shared.Helpers;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UI.Controllers;
using UI.Models;
using UI.Services;

namespace UI.Tests.Controllers;

public class ApiControllerForwardTests
{
    private IGatewayApiClientMock? gateway;
    private ApiController? controller;

    [Before(HookType.Test)]
    public void SetUp()
    {
        gateway = MockGatewayApiClient.Create(Response(HttpStatusCode.OK, "{}"));
        controller = new ApiController(gateway.Object, new StubEnvironment());
    }

    // TUnit0023: ControllerBase is IDisposable — dispose it rather than leaving it to the GC.
    [After(HookType.Test)]
    public void TearDown() => controller?.Dispose();

    private static RestResponse Response(HttpStatusCode status, string? content, string? contentType = null)
        => new() { StatusCode = status, Content = content, ContentType = contentType };

    [Test]
    public async Task ForwardAsync_WhenStatusCodeBelow100_Returns502BadGatewayEnvelope()
    {
        // Arrange — a 0 status means the request never reached the gateway
        var response = Response((HttpStatusCode)0, "ignored");
        response.ErrorMessage = "connection refused";

        // Act
        var result = await controller!.ForwardAsync(() => Task.FromResult(response));

        // Assert
        var objectResult = result as ObjectResult;
        await Assert.That(objectResult).IsNotNull();
        await Assert.That(objectResult!.StatusCode).IsEqualTo(502);
        var envelope = objectResult.Value as ApiResult<dynamic>;
        await Assert.That(envelope).IsNotNull();
        await Assert.That(envelope!.Success).IsFalse();
        var error = envelope.Error as ErrorResult;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).IsEqualTo("connection refused");
    }

    [Test]
    public async Task ForwardAsync_WhenStatusCodeBelow100AndNoErrorMessage_ReturnsDefaultMessage()
    {
        // Arrange — no ErrorMessage set, so the fallback text is used
        var response = Response((HttpStatusCode)0, null);
        response.ErrorMessage = null;

        // Act
        var result = await controller!.ForwardAsync(() => Task.FromResult(response));

        // Assert
        var objectResult = result as ObjectResult;
        await Assert.That(objectResult!.StatusCode).IsEqualTo(502);
        var envelope = objectResult.Value as ApiResult<dynamic>;
        var error = envelope!.Error as ErrorResult;
        await Assert.That(error!.Message).IsEqualTo("Gateway request failed.");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ForwardAsync_WhenContentIsNullOrWhiteSpace_ReturnsStatusCodeResult(string? emptyContent)
    {
        // Arrange — no body to forward, so a bare StatusCodeResult (not a ContentResult)
        var response = Response(HttpStatusCode.NoContent, emptyContent);

        // Act
        var result = await controller!.ForwardAsync(() => Task.FromResult(response));

        // Assert
        var statusResult = result as StatusCodeResult;
        await Assert.That(statusResult).IsNotNull();
        await Assert.That(statusResult!.StatusCode).IsEqualTo(204);
        // A ContentResult would mean the body path was wrongly taken
        await Assert.That(result as ContentResult).IsNull();
    }

    [Test]
    public async Task ForwardAsync_WhenContentPresentWithCustomContentType_PreservesHeaders()
    {
        // Arrange — S4: the gateway's actual content type must be respected, not forced to JSON
        var response = Response(HttpStatusCode.OK, "<xml/>", "application/xml");

        // Act
        var result = await controller!.ForwardAsync(() => Task.FromResult(response));

        // Assert
        var content = result as ContentResult;
        await Assert.That(content).IsNotNull();
        await Assert.That(content!.StatusCode).IsEqualTo(200);
        await Assert.That(content.Content).IsEqualTo("<xml/>");
        await Assert.That(content.ContentType).IsEqualTo("application/xml");
    }

    [Test]
    public async Task ForwardAsync_WhenContentTypeIsNull_DefaultsToApplicationJson()
    {
        // Arrange
        var response = Response(HttpStatusCode.OK, "{}", contentType: null);

        // Act
        var result = await controller!.ForwardAsync(() => Task.FromResult(response));

        // Assert
        var content = result as ContentResult;
        await Assert.That(content!.ContentType).IsEqualTo("application/json");
    }

    [Test]
    public async Task ForwardAsync_WhenSendThrowsException_Returns500WithApiResultEnvelope()
    {
        // Act
        var result = await controller!.ForwardAsync(() =>
            throw new HttpRequestException("gateway down"));

        // Assert — the controller must never leak an exception to the caller
        var objectResult = result as ObjectResult;
        await Assert.That(objectResult).IsNotNull();
        await Assert.That(objectResult!.StatusCode).IsEqualTo(500);
        var envelope = objectResult.Value as ApiResult<dynamic>;
        await Assert.That(envelope!.Success).IsFalse();
        await Assert.That(envelope.Error).IsNotNull();
    }

    [Test]
    public async Task GetUsersAsync_InvokesGatewayUsersEndpoint()
    {
        // Act
        await controller!.GetUsersAsync(CancellationToken.None);

        // Assert
        gateway!.GetAsync("Users", Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task GetUserByUsernameAsync_InvokesGatewayWithUsernameParameter()
    {
        // Act
        await controller!.GetUserByUsernameAsync("Alice", CancellationToken.None);

        // Assert — the username must be interpolated into the path
        gateway!.GetAsync("Users/username/Alice", Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task PostNotificationsAsync_DelegatesModelToGatewayNotificationsEndpoint()
    {
        // Arrange
        var model = new NotificationModel { Id = 7, Title = "t", Content = "c" };

        // Act
        await controller!.PostNotificationsAsync(model, CancellationToken.None);

        // Assert — the same model instance must reach the gateway
        gateway!.PostAsync("Notifications", Arg.Is<object>(o => ReferenceEquals(o, model)), Any<CancellationToken>())
            .WasCalled(Times.Once);
    }

    [Test]
    public async Task GetNotificationHistoriesAsync_InvokesGatewayNotificationHistoriesEndpoint()
    {
        // Act
        await controller!.GetNotificationHistoriesAsync(CancellationToken.None);

        // Assert
        gateway!.GetAsync("NotificationHistories", Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task PostNotificationHistoriesAsync_DelegatesToGatewayNotificationsSendEndpoint()
    {
        // Arrange — note the gateway path is "Notifications/Send", not "NotificationHistories"
        var payload = new { items = new[] { 1L, 2L } };

        // Act
        await controller!.PostNotificationHistoriesAsync(payload, CancellationToken.None);

        // Assert
        gateway!.PostAsync("Notifications/Send", Arg.Is<object>(o => ReferenceEquals(o, payload)), Any<CancellationToken>())
            .WasCalled(Times.Once);
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "UI.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
