using MediatR;

using Microsoft.AspNetCore.Mvc;

using NotificationService.Api.Controllers;
using NotificationService.Application.Commands.DeleteNotificationHistory;
using NotificationService.Application.DTOs;
using NotificationService.Application.Queries.GetNotificationHistory;
using NotificationService.Application.Queries.GetNotificationHistoryById;

using Shared.Helpers;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Api;

public class NotificationHistoriesControllerTests
{
    [Test]
    public async Task GetAllAsync_ReturnsOkWithList()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<IReadOnlyList<NotificationHistoryDto>>>(q => q is GetNotificationHistoryQuery),
                Any<CancellationToken>())
            .Returns(new List<NotificationHistoryDto> { new() { NotificationId = 1, UserId = 10 } });
        var controller = new NotificationHistoriesController(mediator.Object);

        // Act
        var result = await controller.GetAllAsync(CancellationToken.None);

        // Assert
        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<IReadOnlyList<NotificationHistoryDto>>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data!.Count).IsEqualTo(1);
        await Assert.That(envelope.Data[0].UserId).IsEqualTo(10);

        mediator.Send(Arg.Is<IRequest<IReadOnlyList<NotificationHistoryDto>>>(q => q is GetNotificationHistoryQuery),
                Any<CancellationToken>())
            .WasCalled(Times.Once);
    }

    [Test]
    public async Task GetByIdAsync_WhenFound_ReturnsOk()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationHistoryDto?>>(
                q => q is GetNotificationHistoryByIdQuery { NotificationId: 1, UserId: 10 }),
                Any<CancellationToken>())
            .Returns(new NotificationHistoryDto { NotificationId = 1, UserId = 10 });
        var controller = new NotificationHistoriesController(mediator.Object);

        // Act
        var result = await controller.GetByIdAsync(1, 10, CancellationToken.None);

        // Assert
        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<NotificationHistoryDto>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data!.NotificationId).IsEqualTo(1);
        await Assert.That(envelope.Data.UserId).IsEqualTo(10);
    }

    [Test]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<NotificationHistoryDto?>>(
                q => q is GetNotificationHistoryByIdQuery { NotificationId: 999, UserId: 888 }),
                Any<CancellationToken>())
            .Returns((NotificationHistoryDto?)null);
        var controller = new NotificationHistoriesController(mediator.Object);

        // Act
        var result = await controller.GetByIdAsync(999, 888, CancellationToken.None);

        // Assert
        var notFound = await Assert.That(result.Result).IsTypeOf<NotFoundObjectResult>();
        var envelope = (ApiResult<NotificationHistoryDto>)notFound!.Value!;
        await Assert.That(envelope.Success).IsFalse();
        await Assert.That(envelope.Error!.Code).IsEqualTo(404);
        await Assert.That(envelope.Error.Message).IsEqualTo("Notification history not found.");
    }

    [Test]
    public async Task DeleteAsync_WhenFound_ReturnsOk()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<bool>>(
                c => c is DeleteNotificationHistoryCommand { NotificationId: 1, UserId: 10 }),
                Any<CancellationToken>())
            .Returns(true);
        var controller = new NotificationHistoriesController(mediator.Object);

        // Act
        var result = await controller.DeleteAsync(1, 10, CancellationToken.None);

        // Assert
        var ok = await Assert.That(result.Result).IsTypeOf<OkObjectResult>();
        var envelope = (ApiResult<bool>)ok!.Value!;
        await Assert.That(envelope.Success).IsTrue();
        await Assert.That(envelope.Data).IsTrue();
    }

    [Test]
    public async Task DeleteAsync_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var mediator = MockMediator.Create();
        mediator.Send(Arg.Is<IRequest<bool>>(
                c => c is DeleteNotificationHistoryCommand { NotificationId: 999, UserId: 888 }),
                Any<CancellationToken>())
            .Returns(false);
        var controller = new NotificationHistoriesController(mediator.Object);

        // Act
        var result = await controller.DeleteAsync(999, 888, CancellationToken.None);

        // Assert
        var notFound = await Assert.That(result.Result).IsTypeOf<NotFoundObjectResult>();
        var envelope = (ApiResult<bool>)notFound!.Value!;
        await Assert.That(envelope.Success).IsFalse();
        await Assert.That(envelope.Error!.Code).IsEqualTo(404);
        await Assert.That(envelope.Error.Message).IsEqualTo("Notification history not found.");
    }
}