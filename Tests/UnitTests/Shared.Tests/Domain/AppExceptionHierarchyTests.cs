using Shared.Domain.Exceptions;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Domain;

public class AppExceptionHierarchyTests
{
    [Test]
    public async Task AppException_DerivedImplementation_SetsKindAndPublicMessage()
    {
        var exception = new NotFoundException("User", 42);

        await Assert.That(exception.Kind).IsEqualTo(AppErrorKind.NotFound);
        await Assert.That(exception.PublicMessage).IsEqualTo("User 42 not found.");
    }

    [Test]
    public async Task AppException_WithoutDiagnosticMessage_MessageDefaultsToPublicMessage()
    {
        var exception = new NotFoundException("Order", 99);

        await Assert.That(exception.Message).IsEqualTo("Order 99 not found.");
    }

    [Test]
    public async Task AppException_WithInnerException_SetsPropertiesCorrectly()
    {
        var inner = new InvalidOperationException("db constraint");
        var exception = new DuplicateEntityException("User", "User already exists.", inner);

        await Assert.That(exception.Kind).IsEqualTo(AppErrorKind.BadRequest);
        await Assert.That(exception.PublicMessage).IsEqualTo("User already exists.");
        await Assert.That(exception.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task DuplicateEntityException_DefaultConstructor_BuildsFormattedPublicMessage()
    {
        var exception = new DuplicateEntityException("User");

        await Assert.That(exception.EntityName).IsEqualTo("User");
        await Assert.That(exception.PublicMessage).IsEqualTo("User already exists.");
    }

    [Test]
    public async Task DuplicateEntityException_CustomMessageAndInnerException_Preserved()
    {
        var inner = new TimeoutException("connection timeout");
        var exception = new DuplicateEntityException("Product", "Product SKU already taken.", inner);

        await Assert.That(exception.EntityName).IsEqualTo("Product");
        await Assert.That(exception.PublicMessage).IsEqualTo("Product SKU already taken.");
        await Assert.That(exception.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task NotFoundException_SetsEntityAndKeyInPublicMessage()
    {
        var exception = new NotFoundException("Order", "abc-123");

        await Assert.That(exception.EntityName).IsEqualTo("Order");
        await Assert.That(exception.Key).IsEqualTo("abc-123");
        await Assert.That(exception.PublicMessage).IsEqualTo("Order abc-123 not found.");
        await Assert.That(exception.Kind).IsEqualTo(AppErrorKind.NotFound);
    }

    [Test]
    public async Task ValidationFailedException_SetsValidationKindAndPreservesErrors()
    {
        var errors = new List<string> { "Username is required.", "Email is invalid." };
        var exception = new ValidationFailedException(errors);

        await Assert.That(exception.Kind).IsEqualTo(AppErrorKind.Validation);
        await Assert.That(exception.PublicMessage).IsEqualTo("One or more validation errors occurred.");
        await Assert.That(exception.Errors.Count()).IsEqualTo(2);
        await Assert.That(exception.Errors[0]).IsEqualTo("Username is required.");
        await Assert.That(exception.Errors[1]).IsEqualTo("Email is invalid.");
    }

    [Test]
    public async Task ValidationFailedException_WithNullList_InitializesEmptyCollection()
    {
        var exception = new ValidationFailedException((IReadOnlyList<string>)null!);

        await Assert.That(exception.Errors).IsEmpty();
    }
}
