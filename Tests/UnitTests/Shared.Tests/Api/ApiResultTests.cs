using Shared.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Api;

public class ApiResultTests
{
    [Test]
    public async Task Constructor_SuccessAndData_InitializesCorrectly()
    {
        var result = new ApiResult<string>(true, "payload");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Data).IsEqualTo("payload");
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task Constructor_WithErrorCodeAndMessage_InitializesError()
    {
        var result = new ApiResult<string>(true, null, 42, "something went wrong");

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error.Code).IsEqualTo(42);
        await Assert.That(result.Error.Message).IsEqualTo("something went wrong");
    }

    [Test]
    public async Task Constructor_DataAndErrorResult_DefaultsSuccessToFalse()
    {
        var error = new ErrorResult(500, "fail");
        var result = new ApiResult<string>("data", error);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Data).IsEqualTo("data");
        await Assert.That(result.Error).IsEqualTo(error);
    }

    [Test]
    public async Task ErrorResult_NonProductionEnvironment_IncludesExceptionDetails()
    {
        var inner = new InvalidOperationException("inner detail");
        var ex = new InvalidOperationException("outer", inner);
        try { throw ex; } catch { /* capture stack trace */ }
        var error = new ErrorResult(500, "safe message", ex, "Development");

        await Assert.That(error.Code).IsEqualTo(500);
        await Assert.That(error.Message).IsEqualTo("safe message");
        await Assert.That(error.StackTrace).IsNotNull();
        await Assert.That(error.InnerMessage).IsEqualTo("inner detail");
    }

    [Test]
    public async Task ErrorResult_ProductionEnvironment_RedactsDiagnosticDetails()
    {
        var inner = new InvalidOperationException("secret detail");
        var ex = new InvalidOperationException("outer", inner);
        var error = new ErrorResult(500, "safe message", ex, "Production");

        await Assert.That(error.Code).IsEqualTo(500);
        await Assert.That(error.Message).IsEqualTo("safe message");
        await Assert.That(error.StackTrace).IsNull();
        await Assert.That(error.InnerMessage).IsNull();
        await Assert.That(error.InnerStackTrace).IsNull();
    }

    [Test]
    public async Task ErrorResult_NullEnvironmentName_RedactsDiagnosticDetails()
    {
        // F-02: the pre-diff guard was `!string.Equals(environmentName, "Production")`, which a
        // null name satisfies — so an absent environment produced the full exception chain.
        var inner = new InvalidOperationException("secret detail");
        var ex = new InvalidOperationException("outer", inner);
        try { throw ex; } catch { /* capture stack trace */ }
        var error = new ErrorResult(500, "safe message", ex, environmentName: null);

        await Assert.That(error.StackTrace).IsNull()
            .Because("An unknown environment must redact, not disclose");
        await Assert.That(error.InnerMessage).IsNull();
        await Assert.That(error.InnerStackTrace).IsNull();
    }

    [Test]
    public async Task ErrorResult_ExceptionOverload_WithEnvironmentOmitted_RedactsDiagnosticDetails()
    {
        // The exact shape that made the fail-open default dangerous: a caller who forgets the
        // optional environment argument got diagnostics by default.
        var inner = new InvalidOperationException("secret detail");
        var ex = new InvalidOperationException("outer", inner);
        try { throw ex; } catch { /* capture stack trace */ }
        var error = new ErrorResult(500, ex);

        await Assert.That(error.Message).IsEqualTo("outer");
        await Assert.That(error.StackTrace).IsNull()
            .Because("Omitting the environment must be safe, not permissive");
        await Assert.That(error.InnerMessage).IsNull();
        await Assert.That(error.InnerStackTrace).IsNull();
    }

    [Test]
    [Arguments("Staging")]
    [Arguments("Prod")]
    [Arguments("")]
    [Arguments("   ")]
    public async Task ErrorResult_UnrecognisedEnvironmentName_RedactsDiagnosticDetails(string environmentName)
    {
        // Allowlist posture (F-02): anything not explicitly known to be a local diagnostic
        // environment is treated as production-like. "Prod" covers a near-miss on a denylist.
        var inner = new InvalidOperationException("secret detail");
        var ex = new InvalidOperationException("outer", inner);
        try { throw ex; } catch { /* capture stack trace */ }
        var error = new ErrorResult(500, "safe message", ex, environmentName);

        await Assert.That(error.StackTrace).IsNull();
        await Assert.That(error.InnerMessage).IsNull();
        await Assert.That(error.InnerStackTrace).IsNull();
    }

    [Test]
    [Arguments("Development")]
    [Arguments("development")]
    [Arguments("Local")]
    [Arguments("Test")]
    public async Task ErrorResult_RecognisedDiagnosticEnvironment_IncludesDetails_CaseInsensitively(
        string environmentName)
    {
        var inner = new InvalidOperationException("inner detail");
        var ex = new InvalidOperationException("outer", inner);
        try { throw ex; } catch { /* capture stack trace */ }
        var error = new ErrorResult(500, "safe message", ex, environmentName);

        await Assert.That(error.StackTrace).IsNotNull();
        await Assert.That(error.InnerMessage).IsEqualTo("inner detail");
    }

    [Test]
    public async Task ErrorResult_NullException_LeavesDiagnosticsNull_RegardlessOfEnvironment()
    {
        var error = new ErrorResult(500, "safe message", exception: null, environmentName: "Development");

        await Assert.That(error.StackTrace).IsNull();
        await Assert.That(error.InnerMessage).IsNull();
        await Assert.That(error.InnerStackTrace).IsNull();
    }
}
