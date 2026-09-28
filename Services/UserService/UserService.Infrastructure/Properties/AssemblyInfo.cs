using System.Runtime.CompilerServices;

// UserRegisteredEventConsumer.Handle is internal specifically so the consumer's behaviour is
// unit-testable without widening the production type's public API (F-08).
//
// This must be the *attribute* form rather than the <InternalsVisibleTo Include="..." /> item used
// by UI.csproj: this project sets GenerateAssemblyInfo=false, which disables the targets that turn
// that item into this attribute. The item form would be silently ignored here.
[assembly: InternalsVisibleTo("UserService.Tests")]
