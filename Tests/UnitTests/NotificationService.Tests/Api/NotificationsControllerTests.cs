using MediatR;

using Microsoft.AspNetCore.Mvc;

using NotificationService.Api.Controllers;
using NotificationService.Application.Commands.CreateNotification;
using NotificationService.Application.Commands.DeleteNotification;
using NotificationService.Application.Commands.SendNotifications;
using NotificationService.Application.Commands.UpdateNotification;
using NotificationService.Application.DTOs;
using NotificationService.Application.Queries.GetAllNotifications;
using NotificationService.Application.Queries.GetNotificationById;

using Shared.Helpers;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Api;

public class NotificationsControllerTests
{
    [Test]
    public async Task GetAllAsync_ReturnsOkWithList()
    {
        // Arrange
        var mediator = MockMediator.Create();
        var expected = new List<NotificationDto> { new() { Id = 1, Title = "T1" } };
        mediator.Send(Arg.Is<IRequest<IReadOnlyList<NotificationDto>>>(q => q is GetAllNotificationsQuery),
                Any<CancellationToken>())
            .Returns(expected);
        var controller = new NotificationsController(mediator.Object);

        // Act
        var result = await controller.GetAllAsync(CancellationToken.None);

        // Assert
        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<IReadOnlyList<NotificationDto>>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data).IsNotNull();
        await Assert.That(envelope.Data!.Count).IsEqualTo(1);

        mediator.Send(Arg.Is<IRequest<IReadOnlyList<NotificationDto>>>(q => q is GetAllNotificationsQuery),
                Any<CancellationToken>())
            .WasCalled(Times.Once);
    }

    [Test]
    public async Task GetByIdAsync_WhenFound_ReturnsOk()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationDto?>>(q => q is GetNotificationByIdQuery { Id: 5 }),
                Any<CancellationToken>())
            .Returns(new NotificationDto { Id = 5, Title = "T5" });
        var controller = new NotificationsController(mediator.Object);

        // Act
        var result = await controller.GetByIdAsync(5, CancellationToken.None);

        // Assert
        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<NotificationDto>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data!.Id).IsEqualTo(5);
        await Assert.That(envelope.Error).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationDto?>>(q => q is GetNotificationByIdQuery { Id: 999 }),
                Any<CancellationToken>())
            .Returns((NotificationDto?)null);
        var controller = new NotificationsController(mediator.Object);

        // Act
        var result = await controller.GetByIdAsync(999, CancellationToken.None);

        // Assert
        var notFound = await Assert.That(result.Result).IsTypeOf<NotFoundObjectResult>();
        var envelope = (ApiResult<NotificationDto>)notFound!.Value!;
        await Assert.That(envelope.Success).IsFalse();
        await Assert.That(envelope.Data).IsNull();
        await Assert.That(envelope.Error).IsNotNull();
        await Assert.That(envelope.Error!.Code).IsEqualTo(404);
        await Assert.That(envelope.Error.Message).IsEqualTo("Notification not found.");
    }

    [Test]
    public async Task CreateAsync_ReturnsCreatedAtAction()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationDto>>(c => c is CreateNotificationCommand),
                Any<CancellationToken>())
            .Returns(new NotificationDto { Id = 12, Title = "New" });
        var controller = new NotificationsController(mediator.Object);

        // Act
        var result = await controller.CreateAsync(
            new CreateNotificationCommand { Title = "New", Content = "Body" }, CancellationToken.None);

        // Assert
        var created = await Assert.That(result.Result).IsTypeOf<CreatedAtActionResult>();
        // The Location header must point back at the GET action for the new id
        await Assert.That(created!.ActionName).IsEqualTo("GetByIdAsync");
        await Assert.That(created.RouteValues!["id"]).IsEqualTo(12L);
        var envelope = (ApiResult<NotificationDto>)created!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data!.Id).IsEqualTo(12);
    }

    [Test]
    public async Task UpdateAsync_WhenFound_ReturnsOk()
    {
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationDto?>>(c => c is UpdateNotificationCommand { Id: 7 }),
                Any<CancellationToken>())
            .Returns(new NotificationDto { Id = 7, Title = "Updated" });
        var controller = new NotificationsController(mediator.Object);

        var result = await controller.UpdateAsync(7,
            new UpdateNotificationCommand { Title = "Updated", Content = "Body" }, CancellationToken.None);

        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<NotificationDto>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data!.Title).IsEqualTo("Updated");
    }

    [Test]
    public async Task UpdateAsync_WhenNotFound_ReturnsNotFound()
    {
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationDto?>>(c => c is UpdateNotificationCommand { Id: 404 }),
                Any<CancellationToken>())
            .Returns((NotificationDto?)null);
        var controller = new NotificationsController(mediator.Object);

        var result = await controller.UpdateAsync(404,
            new UpdateNotificationCommand { Title = "X", Content = "Y" }, CancellationToken.None);

        var notFound = await Assert.That(result.Result).IsTypeOf<NotFoundObjectResult>();
        var envelope = (ApiResult<NotificationDto>)notFound!.Value!;
        await Assert.That(envelope.Success).IsFalse();
        await Assert.That(envelope.Error!.Code).IsEqualTo(404);
    }

    [Test]
    public async Task DeleteAsync_WhenFound_ReturnsOk()
    {
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<bool>>(c => c is DeleteNotificationCommand { Id: 5 }),
                Any<CancellationToken>())
            .Returns(true);
        var controller = new NotificationsController(mediator.Object);

        var result = await controller.DeleteAsync(5, CancellationToken.None);

        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<bool>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data).IsTrue();
    }

    [Test]
    public async Task DeleteAsync_WhenNotFound_ReturnsNotFound()
    {
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<bool>>(c => c is DeleteNotificationCommand { Id: 999 }),
                Any<CancellationToken>())
            .Returns(false);
        var controller = new NotificationsController(mediator.Object);

        var result = await controller.DeleteAsync(999, CancellationToken.None);

        var notFound = await Assert.That(result.Result).IsTypeOf<NotFoundObjectResult>();
        var envelope = (ApiResult<bool>)notFound!.Value!;
        await Assert.That(envelope.Success).IsFalse();
        await Assert.That(envelope.Error!.Code).IsEqualTo(404);
    }

    [Test]
    public async Task SendAsync_ReturnsOk_AndDiscardsNoResult()
    {
        // N-04: SendNotificationsCommand is a void IRequest. The handler either completes or
        // throws, so there is no failure branch here to test -- the previous BadRequest
        // assertion could only pass by mocking a value the handler could never produce.
        var mediator = MockMediator.Create();
        var controller = new NotificationsController(mediator.Object);

        var result = await controller.SendAsync(
            [new SendNotificationItem { NotificationId = 1, UserId = 2 }], CancellationToken.None);

        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<bool>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data).IsTrue();

        // The command really was dispatched -- a bare Ok would pass this test vacuously.
        // A void IRequest goes through the NON-generic Send(IRequest, CancellationToken)
        // overload, so the matcher is typed SendNotificationsCommand, not IRequest.
        mediator.Send(Arg.Is<SendNotificationsCommand>(cmd => cmd is not null && cmd.Items.Any()), Any<CancellationToken>())
            .WasCalled(Times.Once);
    }
}