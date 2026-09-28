using MediatR;

using Microsoft.AspNetCore.Mvc;

using Shared.Helpers;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Api.Controllers;
using UserService.Application.Commands.DeleteUser;
using UserService.Application.DTOs;
using UserService.Application.Queries.GetAllUsers;
using UserService.Application.Queries.GetUserById;
using UserService.Application.Queries.GetUserByUsername;

namespace UserService.Tests.Api;

public sealed class UsersControllerTests
{
    private IMediatorMock? mediator;
    private UsersController? controller;

    [Before(HookType.Test)]
    public void SetUp()
    {
        mediator = MockMediator.Create();
        controller = new UsersController(mediator.Object);
    }

    [Test]
    public async Task GetAll_ReturnsOkWithUsersList()
    {
        // Arrange
        var users = new List<UserReadDto>
        {
            new UserReadDto { Id = 1, Username = "User1", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new UserReadDto { Id = 2, Username = "User2", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        // Use Arg.Is with predicate to match any GetAllUsersQuery
        var queryArg = Arg.Is<IRequest<IReadOnlyList<UserReadDto>>>(q => q is GetAllUsersQuery);
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(queryArg, tokenArg)
            .Returns(users);

        // Act
        var result = await controller!.GetAllAsync(CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        await Assert.That(okResult).IsNotNull();
        var apiResult = okResult.Value as ApiResult<IReadOnlyList<UserReadDto>>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsTrue();
        await Assert.That(apiResult.Data).IsNotNull();
        await Assert.That(apiResult.Data.Count).IsEqualTo(2);

        // The controller must actually dispatch the query, not fabricate a response.
        mediator!.Send(queryArg, tokenArg).WasCalled(Times.Once);
    }

    [Test]
    public async Task GetById_ReturnsOkWithUser()
    {
        // Arrange
        var user = new UserReadDto { Id = 1, Username = "TestUser", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        // Match on the id too, so a wrong id in the controller would not satisfy the setup.
        var queryArg = Arg.Is<IRequest<UserReadDto?>>(q => q is GetUserByIdQuery { Id: 1 });
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(queryArg, tokenArg)
            .Returns(user);

        // Act
        var result = await controller!.GetByIdAsync(1, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        await Assert.That(okResult).IsNotNull();
        var apiResult = okResult.Value as ApiResult<UserReadDto>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsTrue();
        await Assert.That(apiResult.Data).IsNotNull();
        await Assert.That(apiResult.Data.Id).IsEqualTo(1);
        await Assert.That(apiResult.Data.Username).IsEqualTo("TestUser");

        mediator!.Send(queryArg, tokenArg).WasCalled(Times.Once);
    }

    [Test]
    public async Task GetById_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var queryArg = Arg.Is<IRequest<UserReadDto?>>(q => q is GetUserByIdQuery { Id: 999 });
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(queryArg, tokenArg)
            .Returns((UserReadDto?)null);

        // Act
        var result = await controller!.GetByIdAsync(999, CancellationToken.None);

        // Assert
        var notFound = result.Result as NotFoundObjectResult;
        await Assert.That(notFound).IsNotNull();
        var apiResult = notFound.Value as ApiResult<UserReadDto>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsFalse();
        await Assert.That(apiResult.Error).IsNotNull();
        await Assert.That(apiResult.Error.Code).IsEqualTo(404);
        await Assert.That(apiResult.Error.Message).IsEqualTo("User not found.");

        mediator!.Send(queryArg, tokenArg).WasCalled(Times.Once);
    }

    [Test]
    public async Task GetByUsername_ReturnsOkWithUser()
    {
        // Arrange
        var user = new UserReadDto { Id = 7, Username = "Alice", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var queryArg = Arg.Is<IRequest<UserReadDto?>>(q => q is GetUserByUsernameQuery { Username: "Alice" });
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(queryArg, tokenArg)
            .Returns(user);

        // Act
        var result = await controller!.GetByUsernameAsync("Alice", CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        await Assert.That(okResult).IsNotNull();
        var apiResult = okResult.Value as ApiResult<UserReadDto>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsTrue();
        await Assert.That(apiResult.Data!.Username).IsEqualTo("Alice");

        mediator!.Send(queryArg, tokenArg).WasCalled(Times.Once);
    }

    [Test]
    public async Task GetByUsername_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var queryArg = Arg.Is<IRequest<UserReadDto?>>(q => q is GetUserByUsernameQuery { Username: "Ghost" });
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(queryArg, tokenArg)
            .Returns((UserReadDto?)null);

        // Act
        var result = await controller!.GetByUsernameAsync("Ghost", CancellationToken.None);

        // Assert
        var notFound = result.Result as NotFoundObjectResult;
        await Assert.That(notFound).IsNotNull();
        var apiResult = notFound.Value as ApiResult<UserReadDto>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsFalse();
        await Assert.That(apiResult.Error).IsNotNull();
        await Assert.That(apiResult.Error.Code).IsEqualTo(404);
        await Assert.That(apiResult.Error.Message).IsEqualTo("User not found.");

        mediator!.Send(queryArg, tokenArg).WasCalled(Times.Once);
    }

    [Test]
    public async Task Delete_WhenUserExists_ReturnsOkWithSuccessResult()
    {
        // Arrange
        var cmdArg = Arg.Is<IRequest<bool>>(q => q is DeleteUserCommand { Id: 1 });
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(cmdArg, tokenArg)
            .Returns(true);

        // Act
        var result = await controller!.DeleteAsync(1, CancellationToken.None);

        // Assert — the controller returns Ok(ApiResult<bool>), not NoContentResult.
        var okResult = result.Result as OkObjectResult;
        await Assert.That(okResult).IsNotNull();
        var apiResult = okResult.Value as ApiResult<bool>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsTrue();
        await Assert.That(apiResult.Data).IsTrue();

        mediator!.Send(cmdArg, tokenArg).WasCalled(Times.Once);
    }

    [Test]
    public async Task Delete_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var cmdArg = Arg.Is<IRequest<bool>>(q => q is DeleteUserCommand { Id: 999 });
        var tokenArg = Arg.Any<CancellationToken>();
        mediator!.Send(cmdArg, tokenArg)
            .Returns(false);

        // Act
        var result = await controller!.DeleteAsync(999, CancellationToken.None);

        // Assert
        var notFound = result.Result as NotFoundObjectResult;
        await Assert.That(notFound).IsNotNull();
        var apiResult = notFound.Value as ApiResult<bool>;
        await Assert.That(apiResult).IsNotNull();
        await Assert.That(apiResult.Success).IsFalse();
        await Assert.That(apiResult.Error).IsNotNull();
        await Assert.That(apiResult.Error.Code).IsEqualTo(404);
        await Assert.That(apiResult.Error.Message).IsEqualTo("User not found.");

        mediator!.Send(cmdArg, tokenArg).WasCalled(Times.Once);
    }
}
