using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UI.Controllers;
using UI.Models;

namespace UI.Tests.Controllers;

public class HomeControllerTests
{
    private HomeController? controller;
    private DefaultHttpContext? httpContext;

    [Before(HookType.Test)]
    public async Task SetUp()
    {
        controller = new HomeController(NullLogger<HomeController>.Instance);
        httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-0HN7:00000001";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    // TUnit0023: ControllerBase is IDisposable — dispose it rather than leaving it to the GC.
    [After(HookType.Test)]
    public void TearDown() => controller?.Dispose();

    [Test]
    public async Task Index_ReturnsViewResult()
    {
        // Act
        var result = controller!.Index();

        // Assert
        var view = result as ViewResult;
        await Assert.That(view).IsNotNull();
        await Assert.That(view!.ViewName).IsNull();   // no view name => conventional view resolution
    }

    [Test]
    public async Task Privacy_ReturnsViewResult()
    {
        // Act
        var result = controller!.Privacy();

        // Assert
        await Assert.That(result as ViewResult).IsNotNull();
    }

    [Test]
    public async Task Error_ReturnsViewResultWithTraceIdentifier()
    {
        // ASP.NET Core populates Activity.Current, and Error() prefers it over
        // HttpContext.TraceIdentifier. Clear it to exercise the fallback branch deterministically.
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            // Act
            var result = controller!.Error();

            // Assert
            var view = result as ViewResult;
            await Assert.That(view).IsNotNull();
            var model = view!.Model as ErrorViewModel;
            await Assert.That(model).IsNotNull();
            await Assert.That(model!.RequestId).IsEqualTo("trace-0HN7:00000001");
            await Assert.That(model.ShowRequestId).IsTrue();
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Test]
    public async Task Error_PrefersActivityTraceId_WhenAnActivityIsCurrent()
    {
        // Arrange
        using var activity = new Activity("test-activity").Start();

        // Act
        var result = controller!.Error();

        // Assert — Activity.Current?.Id wins over HttpContext.TraceIdentifier
        var model = (result as ViewResult)!.Model as ErrorViewModel;
        await Assert.That(model).IsNotNull();
        await Assert.That(model!.RequestId).IsEqualTo(Activity.Current!.Id);
    }
}
