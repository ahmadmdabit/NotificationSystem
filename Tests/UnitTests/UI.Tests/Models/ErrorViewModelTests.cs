using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UI.Models;

namespace UI.Tests.Models;

public class ErrorViewModelTests
{
    [Test]
    public async Task ShowRequestId_WhenPopulated_ReturnsTrue()
    {
        // Arrange
        var model = new ErrorViewModel { RequestId = "0HN7:00000001" };

        // Act
        var show = model.ShowRequestId;

        // Assert
        await Assert.That(show).IsTrue();
        await Assert.That(model.RequestId).IsEqualTo("0HN7:00000001");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task ShowRequestId_WhenNullOrEmpty_ReturnsFalse(string? id)
    {
        // Arrange
        var model = new ErrorViewModel { RequestId = id! };

        // Act
        var show = model.ShowRequestId;

        // Assert — ShowRequestId is !string.IsNullOrEmpty(RequestId), so null and "" are both false
        await Assert.That(show).IsFalse();
    }

    [Test]
    public async Task ShowRequestId_WhenChanged_ReflectsCurrentValue()
    {
        // Arrange
        var model = new ErrorViewModel { RequestId = string.Empty };

        // Act
        model.RequestId = "trace-1";

        // Assert — ShowRequestId is computed, not cached
        await Assert.That(model.ShowRequestId).IsTrue();
    }
}
