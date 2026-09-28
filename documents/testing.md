# Development

[Back to README](../README.md)

## Testing

Unit tests target the Application, Domain, Infrastructure, and Api layers, using **TUnit 1.69.0** (no NUnit, FluentAssertions, Moq, or NSubstitute in test code). Four test assemblies plus a shared TestDoubles project, and two contract suites:

- **`UserService.Tests`** — commands, queries, validators, domain aggregates, infrastructure (repositories, PasswordHasher, JwtTokenService, consumer)
- **`NotificationService.Tests`** — commands, queries, validators, domain aggregates, infrastructure (repositories, TVP streaming), controllers
- **`Shared.Tests`** — ApiResult, ApiExceptionHandler, MessagingHealthCheck, pipeline behaviors (validation, logging, transaction), UnitOfWork, DomainEventCollector, `SqlCommands`, AppException hierarchy
- **`UI.Tests`** — controllers, `GatewayApiClient` token lifecycle (fast-path cache, 401 refresh, register-then-reauth, malformed JSON, idempotent 400), `Startup` DI wiring, `ErrorViewModel`
- **`TestDoubles`** — mocks, stubs and helpers shared by all four test assemblies (which never reference each other)
- **`WiringTests`** — DI resolution (including a mirror of each `Program.cs` API registration) **and** stored-procedure contract guards that scan production source text; the reason a pure refactor can break a test
- **`ArchitectureTests`** — ArchUnitNET dependency rules via the `TngTech.ArchUnitNET.TUnit` adapter. ArchUnitNET analyses IL, so a **Debug** build is required for it to see real instructions; the suite is currently also discovered and green under `-c Release`, but only Debug is a meaningful run.

## Two guards worth knowing about

Both of these look like removable noise and are not.

- **A stored-procedure guard asserts on the migration _source_, not on SQL behaviour.**
  `StoredProcedureContractTests.Sp_NotificationHistoryInsert_IsIdempotent_For_A_RepeatPair` reads
  `DatabaseMigration.cs` and asserts the history insert is filtered with `WHERE NOT EXISTS` on both
  key columns. That stops someone deleting the guard. It does **not** prove the procedure behaves
  correctly when run — proving that needs a live database. The test says so in a comment, and so
  should any future one.
- **A DI guard that mirrors a `Program.cs` has to cover _both_ services.**
  `DependencyInjectionTests.Messaging_Disabled_ResolvesADispatcherAndRegistersNoBus` is
  parameterised over UserService and NotificationService, because the two `DependencyInjection`
  files contain the same `if/else` around `IDomainEventDispatcher` and only one of them was
  originally covered. Deleting NotificationService's `else` branch makes the `true` case fail and
  the `false` case pass — which is the whole diagnostic value of parameterising it.

## Verifying a test actually guards something

A test that asserts on a string can pass for the wrong reason, so the three guards added for the
idempotency and messaging work were each broken on purpose and confirmed red:

| Guard                      | What was broken                                              | Result                                                                                     |
| -------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| History-insert idempotency | the asserted literal in the test                             | `Sp_NotificationHistoryInsert_IsIdempotent_For_A_RepeatPair` red                           |
| Messaging-disabled DI      | deleted NotificationService's `else` branch (**production**) | `Messaging_Disabled_ResolvesADispatcherAndRegistersNoBus(True)` red, `(False)` still green |
| Affected-row trim          | reverted the clamp to `toSend` (**production**)              | `Handle_WhenConcurrentRequestWinsTheDraftRace_...` red                                     |

The second and third are the ones that matter: they were proved by breaking the
implementation, not the assertion. A mutation that edits the test only proves the test can read
its own string.

## TUnit rules this codebase has tripped over

- **A void `IRequest` is verified with a matcher typed to the concrete command, not to `IRequest`.**
  MediatR routes a void request through the non-generic `Send(IRequest, CancellationToken)`, so
  `Arg.Is<IRequest>(...)` compiles and then matches nothing (`called 0 time(s)`).
- **`Times.Once` is a property, not `Times.Once()`.** `VerifyLog()` / `VerifyNoLog()` are extension
  methods in `TUnit.Mocks.Logging`, so the `using` is required.
- **`DomainEventCollector` is static ambient state** — clear it in `[After]` where a test touches it.
  TUnit gives a fresh class instance per test but does not reset statics, and tests run in parallel.
- **Never assert against the constant a value was built from.** `Assert.That(sql).Contains(SomeColumnsConstant)`
  is circular — mutating the constant leaves the test green.
- **`--no-incremental` is a `dotnet build` flag.** Passing it to `dotnet test` gives `total: 0, error: 7`
  and no message. Use `dotnet build --no-incremental` then `dotnet test --no-build`.
- **A test that reads process-wide mutable state needs `[NotInParallel]`.** TUnit runs tests in
  parallel by default. If a test reflects into a static (or reads a static cache) that other tests in
  the same assembly mutate, it is racing them — `[NotInParallel]` with no keys makes it run alone.
- **`Assert.That(object)` is type-strict.** A reflection or boxed value compared against a differently-typed
  expectation fails even when the numbers match. Compare numerically when the value came out of
  reflection.
- **After any mutate/restore cycle, re-read the file to confirm the restore, then rebuild before
  trusting a suite result.** A test run started right after a restore can race it and report the
  mutants as genuine failures.

> ✅ **One test used to be intermittent here, and the cause is worth knowing.**
> `Dapper_DateTime_Maps_To_DateTime2_And_Configure_Is_Idempotent` read Dapper's private static
> `typeMap` by reflection while every other test in its class called `DapperConfiguration.Configure()`,
> mutating that same dictionary — a reader racing its own writers. It failed **1 run in 5** at the
> solution level and **0 in 7** when the project was run alone. Two things were wrong, and both
> mattered: the read was unsynchronised, _and_ the assertion compared a boxed value type-strictly,
> so `expected DateTime2 but received -2` was a **type-identity** message, not a wrong number
> (`DbType.DateTime2` **is** `-2`). The test is now `[NotInParallel]` and compares the numeric value.
>
> The generalisable lesson: if an assertion message says `expected X but received <X's own value>`,
> suspect the runtime _type_ of the comparison before you go hunting for a value bug.

## The DI guard, and why it eagerly resolves

`DependencyInjectionTests.*_ApiRegistrations_AllResolve` mirrors the registration block of each
service's `Program.cs` and **actively resolves** `IExceptionHandler`. It exists because of a real
defect: `ApiExceptionHandler` constructor once took a bare `string` instead of
`IHostEnvironment`, which compiled cleanly, started cleanly, and threw the first time an unhandled
exception needed translating — i.e. exactly when the `ApiResult` envelope had to be produced.

Two non-obvious things about it, both of which will look like removable noise:

- **`ValidateOnBuild` is deliberately OFF for the API scope.** It is not enough _and_ it is
  actively misleading here. `AddExceptionHandler<T>` registers a lazily-constructed singleton, so
  graph validation inspects the descriptor and never calls the constructor — the defect stays
  invisible. Meanwhile validation _false-positives_ on MVC's own descriptors
  (`ControllerActionInvokerProvider`, `ControllerRequestDelegateFactory`, …), which depend on
  services `WebApplicationBuilder` supplies but a bare `ServiceCollection` does not. Removing the
  eager resolve re-opens the blind spot; enabling `ValidateOnBuild` buries the real signal in
  framework noise. `ValidateScopes` stays on.
- **`IWebHostEnvironment` and `IHostEnvironment` are registered explicitly**, pointing at one local
  stub, because MVC's registrations consume `IWebHostEnvironment`. `MessagingHealthCheck` is
  asserted through `IOptions<HealthCheckServiceOptions>`, not by resolving the type — `AddCheck<T>(name)`
  registers an _instance_, so the type is not resolvable as a service at all.

Run all tests:

```bash
dotnet test NotificationSystem.slnx -c Release
```

> ⚠️ **The solution-wide command exits `8`, not `0`.** `TestDoubles` is a class library with no tests, and `global.json`'s `test` node supports only `runner`, so a project cannot be excluded. **Check `failed: 0` rather than the exit code.** Running an individual test project does exit 0, if a script needs a hard gate.
>
> **Do not try to "fix" this.** Three exclusion attempts were made and all three were reverted: a `testconfig.json` filter, a solution filter, and reclassifying `TestDoubles` as a non-test library. `global.json` cannot exclude a project, so the exit code is Microsoft.Testing.Platform's project-selection behaviour, not a defect in this repository. If a CI gate keys on the exit code it will be permanently red, with a three-second "Zero tests ran" line as the only clue.

Test conventions worth knowing before writing a test:

- Assertions must be **awaited** — `await Assert.That(…)`. This is a compile error, so a `void` test method cannot contain one; use the static `Assert.Throws<T>(…)` there.
- **Verify every mock you inject** (`WasCalled(Times.Once)`). Stubbing alone does not prove the dependency was used.
- **Mutation-verify new or rewritten tests** — break the production code, confirm the test fails, then revert. This repo shipped 106 tests with empty bodies that all passed, and a single mutation later exposed a live production defect that three green tests had missed.
- **Repository tests assert the `CommandDefinition`s**, not the connection: Dapper's query methods are static extensions on `IDbConnection` and cannot be intercepted. Row mapping needs a live provider and is deliberately out of scope.
- **TUnit specifics that will otherwise cost a cycle:** `Times.Once` is a property, not a method; `VerifyLog()` / `VerifyNoLog()` are extension methods in `TUnit.Mocks.Logging` (the `using` is required even though `Mock.Logger<T>()` resolves); and `dotnet test --filter` reports `Zero tests ran` under Microsoft.Testing.Platform — run the project and read the summary instead.
- **Watch for vacuous assertions when testing serialization.** `DefaultHttpContext.Response.Body` is `Stream.Null`, so anything written to it is discarded and every `DoesNotContain` against the body passes for free. Likewise an exception that is only `new`-ed has a null `StackTrace`. Both shapes produced green tests that asserted nothing before an explicit non-vacuity check was added.

Collect coverage. TUnit runs on **Microsoft.Testing.Platform**, not VSTest, so there is no
`dotnet-coverage`/`.runsettings` step — `--coverage` is a first-class flag on the test host:

```bash
# Per test project (run each; coverage is per-assembly-set, not solution-wide)
cd Tests/UnitTests/Shared.Tests
dotnet run -c Release --coverage

# Other formats / explicit output directory
dotnet run -c Release --coverage --coverage-output-format cobertura --coverage-output-format xml
```

Results land in `bin/Release/net10.0/TestResults/` (an HTML report and a `.tunit-report.json` are
written alongside the Cobertura file). `--coverage-output` is resolved relative to the test output
directory, not the project directory, so pass an absolute path if you want it elsewhere.

> **Measured line coverage (Shared.Tests, Release):** `Shared.Domain` 92.5%, `Shared.Application`
> 90.3%, `Shared.Api` 89.0%, `Shared.Infrastructure` 65.5%. These are the real numbers as of this
> commit — an earlier "99–100%" claim in this file was not reproducible by any workflow the README
> described, and has been replaced rather than restated. Re-measure before quoting a figure.

## Adding New Features

1. Define the entity in the service's `Domain/Entities/` folder — add a `Rehydrate(...)` factory if its identity is DB-assigned
2. Create repository interface in `Domain/Abstractions/`
3. Implement concrete repository in `Infrastructure/Repositories/` — **execution only**
4. Add a `*CommandFactory` in `Infrastructure/Persistence/` holding that repository's `CommandDefinition` builders, reusing `SqlCommands` for the shared shapes
5. Create DTO in `Application/DTOs/`
6. Create command/query in `Application/Commands/` or `Application/Queries/`
7. Create handler in same directory - implement `IRequestHandler<TCommand, TResponse>`, or `IRequestHandler<TCommand>` for a void command with no meaningful result
8. Create FluentValidation validator in same directory — include a case **at** each limit, not only over it
9. Add controller in `Api/Controllers/`, delegate to `IMediator`
10. Register services in `Api/Program.cs`
11. Add routing in `Services/ApiGateway/ocelot.json` **under the `Routes` array** (single source; `ocelot.Development.json` overrides for local dev). Do not use the legacy `ReRoutes` key — Ocelot 25.x ignores it and every gateway call returns 404
12. Create UI pages if needed
13. Add unit tests in `Tests/UnitTests/<Assembly>/...` (mocks/stubs/helpers in `TestDoubles/`), and mutation-verify them
