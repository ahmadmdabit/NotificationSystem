using FluentValidation;

using MediatR;

using Shared.Application.Behaviors;
using Shared.Domain.Exceptions;

using TestDoubles.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Application;

public class ValidationBehaviorTests
{
    [Test]
    public async Task Handle_WithoutValidators_ExecutesNextDelegate()
    {
        var behavior = new ValidationBehavior<ValidationTestRequest, ValidationTestResponse>(
            Enumerable.Empty<IValidator<ValidationTestRequest>>());

        var result = await behavior.Handle(
            new ValidationTestRequest("user", "a@b.com"),
            ValidationTestFixtures.OkDelegate,
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(new ValidationTestResponse("ok"));
    }

    [Test]
    public async Task Handle_ValidationPasses_ExecutesNextDelegate()
    {
        var behavior = new ValidationBehavior<ValidationTestRequest, ValidationTestResponse>(
            [new ValidationTestFixtures.ValidationTestValidator()]);

        var result = await behavior.Handle(
            new ValidationTestRequest("user", "a@b.com"),
            ValidationTestFixtures.OkDelegate,
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(new ValidationTestResponse("ok"));
    }

    [Test]
    public async Task Handle_ValidationFails_ThrowsValidationFailedExceptionWithAggregatedErrors()
    {
        var behavior = new ValidationBehavior<ValidationTestRequest, ValidationTestResponse>(
            [new ValidationTestFixtures.ValidationTestValidator()]);

        RequestHandlerDelegate<ValidationTestResponse> next = _ =>
            Task.FromResult(new ValidationTestResponse("should not reach"));

        // Use an async lambda: the delegate assertion expects Task, not Task<TResponse>.
        var ex = await Assert.That(async () =>
            {
                await behavior.Handle(new ValidationTestRequest("", ""), next, CancellationToken.None);
            })
            .Throws<ValidationFailedException>();

        await Assert.That(ex!.Kind).IsEqualTo(AppErrorKind.Validation);
        await Assert.That(ex!.Errors.Count()).IsEqualTo(2);
        await Assert.That(ex!.Errors.First(e => e.Contains("Username"))).IsNotNull();
        await Assert.That(ex!.Errors.First(e => e.Contains("Email"))).IsNotNull();
    }
}
