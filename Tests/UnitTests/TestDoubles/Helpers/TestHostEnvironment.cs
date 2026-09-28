using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace TestDoubles.Helpers;

/// <summary>
/// <see cref="IHostEnvironment"/> test double for types that take the interface — currently
/// <c>Shared.Api.ApiExceptionHandler</c>, whose constructor argument was a bare
/// <c>string environmentName</c> until F-01. The interface is what the DI container actually
/// registers, so injecting it keeps the test honest about the production wiring.
/// </summary>
/// <remarks>
/// The file providers default to <see cref="NullFileProvider"/>: nothing under test reads content
/// or web roots, and a real provider would make this double depend on the filesystem.
/// <c>WiringTests</c> keeps its own private copy of an equivalent stub rather than referencing
/// this library, to preserve the project's test-project independence rule.
/// </remarks>
public sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "TestDoubles";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string? WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
