# Conventions

[Back to README](../README.md)

The rules below are the ones that are **not** obvious from reading the code, together with the
reason each exists. How-to material lives elsewhere: [Architecture](architecture.md),
[Getting Started](getting-started.md), [API Reference](api-reference.md),
[Database](database.md), [Messaging & Dispatch](messaging.md),
[Development](testing.md), [Operations](operations.md).

Where a rule exists because something broke, the failure is named. A convention with no stated
cost tends to get "simplified" away the first time it is inconvenient.

## Architecture

### Layering

Clean Architecture with CQRS and DDD. Dependency direction is
`Domain` → `Application` → `Infrastructure` → `Api`, and it is enforced by ArchUnitNET rather
than by convention alone — see [Testing](#testing).

- **Domain** — rich entities with behaviour (`User.Create()`, `Notification.MarkAsSent()`),
  value objects, domain events, repository interfaces, `IUnitOfWork`, `IDomainEventDispatcher`.
  No Dapper or EF attributes, and no framework dependency beyond `Shared.Domain`.
- **Application** — CQRS commands/queries over MediatR, DTOs, FluentValidation, pipeline
  behaviors. The `ICommand` marker selects which requests get a transaction.
- **Infrastructure** — Dapper repositories, `UnitOfWork`, `JwtTokenService`, `PasswordHasher`,
  MassTransit configuration, TVP definitions, embedded SQL resources.
- **Api** — controllers that delegate to `IMediator` and nothing else, plus JWT auth and the
  `ApiResult<T>` envelope.

A shared kernel lives in `Shared/` (`Domain`, `Application`, `Infrastructure`, `Api`). It
references neither service, and a service may not be referenced _from_ the kernel — that is
what keeps the direction intact.

### Commands with no result

**A command with no meaningful result is a void `IRequest` + `IRequestHandler<T>`, not
`IRequest<T>` returning a constant.** `SendNotificationsCommand` was `IRequest<bool>` whose
handler could only ever return `true`, which produced an unreachable `else BadRequest` in the
controller. A void request is routed by MediatR through the **non-generic**
`Send(IRequest, CancellationToken)` overload, which changes how mocks are verified — see
[Testing](#testing). The HTTP response body stays `ApiResult<bool>(true, true)`, so the wire
contract is unaffected by the refactor.

### Domain events are dispatched after commit

Aggregates raise events into a list; the transaction pipeline publishes them only after the
commit succeeds, so a rollback can never leave a published event behind.

1. Handlers record events on the `DomainEventCollector`, an `AsyncLocal` that isolates
   concurrent flows per request.
2. `TransactionBehavior` wraps every `ICommand` in an `IUnitOfWork` transaction and calls
   `DomainEventCollector.Seed()` **before** invoking the handler.
3. Only after a successful commit does it `Drain()` and publish via `IDomainEventDispatcher`.
4. On rollback it `Clear()`s — pending events are never published.

> ⚠️ **`Seed()` is load-bearing — never remove it.** `AsyncLocal` values flow _into_ an awaited
> callee, but a mutation made inside that callee does **not** flow back to the caller. Without
> seeding, the handler's `Add()` hits `_pending.Value ??= []` and allocates a list the pipeline
> cannot see, so the post-commit `Drain()` returns empty and **every domain event is silently
> dropped**. This was a live production defect. Three existing tests missed it because they

## Persistence

### SQL lives in a command factory, never inline in a repository

`Persistence/*CommandFactory.cs` builds the Dapper `CommandDefinition`s; the repository only
executes them. This is not tidiness — it is the only way to test the SQL contract at all.
Dapper's `QuerySingleOrDefaultAsync` / `ExecuteAsync` are **static extension methods** on
`IDbConnection`, not interface members, so no test double on the connection can intercept
them, and a generated `DbCommand` mock exposes neither `CreateParameter()` nor `Parameters`
(both protected). The built command is the observable unit.

Shared shapes that are genuinely identical across services — `SelectByKey`, `SelectAll`,
`SelectIn`, `SoftDeleteByKey` — live in `Shared.Infrastructure.Persistence.SqlCommands` so the
soft-delete convention has exactly one definition and a new table cannot accidentally omit the
`IsDeleted = 0` guard. Stored-procedure calls stay in the per-service factory: their parameter
shapes differ per domain (explicit `DynamicParameters` with sizes, TVPs, output parameters), and
folding them together would mean a generic parameter bag.

### Identifiers are validated, because they cannot be parameterised

Table and column names are interpolated into SQL, so they are the one thing parameter binding
cannot protect. **No caller ever supplies a SQL fragment** — callers pass identifiers and the
matching parameter object, and `SqlCommands` builds every predicate.

`ValidateIdentifier` enforces a bare identifier with a compiled regex **anchored `\A…\z`, not
`^…$`**. In .NET a bare `$` also matches immediately before a trailing newline, so `^[a-z]+$`
accepts `"Users\n"` — which is then interpolated into the statement. That was a real bypass
here, caught by adding the trailing-newline case to the rejection set.

### Writes against a natural key must be idempotent

`NotificationHistories` has `PRIMARY KEY (NotificationId, UserId)`. An unguarded
`INSERT ... SELECT` therefore turns a repeat send into a PK violation, which the SP reports as
`@SPSuccess = 0`, which the repository converts to an exception, which the API returns as
**HTTP 500**. `SPNotificationHistoryInsert` filters the TVP with `WHERE NOT EXISTS` on both key
columns; soft-deleted rows still occupy the key, so the guard covers them too.

The general rule: **if a table has a natural key, the write must tolerate repeats** — or the
caller must de-duplicate across requests, not merely within one. De-duplicating inside the
handler's `GroupBy` protects one request and nothing more.

### Hydration goes through a private row type

`NotificationRepository` materialises a private `NotificationRow` and calls
`Notification.Rehydrate`, rather than letting Dapper construct the aggregate. The trade is
explicit: Dapper maps **by name** and silently assigns `default` for a name it does not find,
so a typo between the row DTO and `NotificationColumns` yields a half-populated entity and no
error. A test asserts the two name sets are identical, naming both explicitly rather than
reading the production constant. `UserRepository` follows the same pattern, and there the row
carries raw `PasswordHash` / `PasswordSalt` — which is exactly why it must stay `private`:
a public row type would put credential columns on the assembly's public API surface.

Aggregates whose identity is database-assigned need a `Rehydrate(...)` factory. `Notification.Create`
always yields `Id = 0`, so any handler filtering on a positive id would be untestable without it.

### A set-based batch UPDATE reports a count, not an identity

`MarkSentBatchAsync` returns the affected-row count. That is enough to detect that a concurrent
send won the `Status = Draft` race, but not to say _which_ ids lost — so the handler trims by
count and logs the discrepancy. Closing this properly needs `OUTPUT INSERTED.Id`. Revisit if
`NotificationSentEvent` ever gains a consumer; today it has none, so the behaviour is
unobservable downstream.

## API and error contract

- **Controllers delegate to `IMediator` and let the exception pipeline translate.** A local
  `try/catch` around a command is a second, divergent translation path.
- **Diagnostic detail is allowlisted, never denylisted.** `ErrorResult` attaches
  `StackTrace` / `InnerMessage` / `InnerStackTrace` only in `Development`, `Local` and `Test`.
  The earlier `!= "Production"` test was fail-**open**: a `null` name satisfied it and emitted
  the full exception chain. Adding a new environment name means adding it to
  `DiagnosticEnvironments`, which fails safe by default.
- **A constructor on a DI-registered type takes interfaces, never primitives.**
  `ApiExceptionHandler(ILogger<T>, IHostEnvironment)` — a `string` parameter compiles, starts
  fine, and throws only when the error path first needs it. Do not "fix" that with
  `AddSingleton<string>`: it is ambiguous the moment a second consumer needs one, and it leaves
  a security-relevant value (`EnvironmentName`, which gates redaction) resolvable as an
  untyped primitive.
- **Serialised DTOs get settable members only.** A get-only property is still serialised by
  `System.Text.Json` and ships its `default` — which is how a dead `UtcNow` property emitted
  `0001-01-01T00:00:00` in every registration response.
  > added events _before_ entering the pipeline, which pre-creates the list.
  > `TransactionBehaviorTests.Handle_HandlerAddsEventInsideNext_PublishesItPostCommit` is the
  > regression guard — any test of pipeline behaviour must add its events _inside_ the `next()`
  > delegate, exactly as production does. A handler tested in isolation must call `Seed()` in its
  > own Arrange for the same reason.

## Testing

TUnit 1.69.0 throughout. No NUnit, FluentAssertions, Moq or NSubstitute in test code. Six
projects: four unit-test assemblies, a shared `TestDoubles` library, `WiringTests` and
`ArchitectureTests` — see [Development](testing.md) for what each covers.

**The four unit-test projects reference `TestDoubles` only — never each other.** That
independence is the point: a shared helper that couples two suites turns one project's
refactor into another's breakage.

### Rules that are easy to get wrong

- **Assertions must be awaited** (`await Assert.That(…)`). This is a compile _error_
  (`TUnitAssertions0002`), so a `void` test method cannot contain one — use the static
  `Assert.Throws<T>(…)` there, which returns the exception rather than a `Task`.
- **Verify every mock you inject.** `WasCalled(Times.Once)` / `WasNeverCalled()` are the reason
  to use TUnit.Mocks at all; a stub-only test does not prove the dependency was used.
- **A void `IRequest` is verified with a matcher typed to the concrete command**, not to
  `IRequest`. MediatR routes it through the non-generic `Send(IRequest, CancellationToken)`;
  `Arg.Is<IRequest>(...)` compiles and silently matches nothing (`called 0 time(s)`).
- **Never assert against the constant a value was built from.**
  `Assert.That(sql).Contains(SomeColumnsConstant)` is circular — mutating the constant leaves
  the test green.
- **Validation rules need a case at the exact limit**, not only over it. `MaximumLength(n)`
  and `Count() <= n` are inclusive, so the boundary itself is a distinct case.
- **`DomainEventCollector` is static ambient state.** TUnit gives a fresh class instance per
  test but does not reset statics, and tests run in parallel — clear it in `[After]` anywhere a
  test touches it.
- **A test that reads process-wide mutable state must be `[NotInParallel]`.** TUnit runs tests
  in parallel by default; a test reflecting into a static that other tests mutate is a reader
  racing its own writers. `Dapper_DateTime_Maps_To_DateTime2_And_Configure_Is_Idempotent` did
  exactly this and failed 1 run in 5 at solution level while never failing when its project was
  run alone.
- **`Assert.That(object)` is type-strict, and the message will not tell you so.** A failure
  reading `expected DateTime2 but received -2` is not a wrong number — `DbType.DateTime2` **is**
  `-2`. When a value arrives from reflection or a boxed cast, compare the numeric value and
  check that expected and actual are the same runtime type before chasing a value bug.
- **Watch for vacuous assertions.** `DefaultHttpContext.Response.Body` is `Stream.Null`, so
  anything written to it is discarded and a `DoesNotContain` on a serialised body passes for
  free — set a real `MemoryStream` and assert non-empty first. An exception that is only
  `new`-ed has a null `StackTrace`; throw and catch it when the test asserts diagnostics.
- **State the limits you do not have coverage for.** Row mapping and the Dapper execute call
  need a live ADO.NET provider and are not unit-testable; the command shapes are. Say so in the
  test file rather than implying coverage you do not have.

### Mutation-verify every new or rewritten test

Break the production code, confirm the intended test fails, revert. A green suite proves
nothing on its own: this repository once carried 106 passing tests with empty method bodies, and
a single mutation later exposed a live defect that three green tests had missed.

**Prefer breaking the _production_ code over weakening the assertion.** A mutation that edits
the test proves only that the test can read its own string. And **hardening an assertion is not
a substitute for fixing a race** — loosening a comparison would have made the Dapper test pass
while it read an inconsistent dictionary, converting a visible flake into an invisible bug.

Write a guard for **both** directions of a clamp or a trim: a `Take(affected)` fix needs a test
proving the surplus is dropped _and_ a test proving nothing is dropped when the full count is
affected, or it can over-correct into silent event loss.

After any mutate/restore cycle, verify the restore by reading the file and rebuild
non-incrementally before trusting a suite result — a `dotnet test` started immediately after the
restore can race it and report the mutants as genuine failures.

### Gating

```bash
dotnet test NotificationSystem.slnx -c Release     # everything
dotnet test Tests/ArchitectureTests/ArchitectureTests.csproj -c Debug
```

> ⚠️ **The solution-wide run exits `8`, not `0`.** `TestDoubles` is a library with no tests and
> `global.json`'s `test` node cannot exclude a project. **Gate on `failed: 0`, never on the exit
> code.** Per-project runs do exit `0` if a script needs that.

`--no-incremental` is a `dotnet build` flag. Passing it to `dotnet test` yields
`total: 0, error: 7` and no message in either stream. Use
`dotnet build --no-incremental` then `dotnet test --no-build`.

`dotnet test --filter` reports `Zero tests ran` under Microsoft.Testing.Platform (exit 5). Run
the project and read the summary.

### What a guard must actually pin

- **DI guards mirror each `Program.cs`** and must cover **both** services. The two
  `DependencyInjection` files contain the same messaging `if/else`, and a guard covering only
  one left a real gap: deleting the `NullDomainEventDispatcher` branch in NotificationService
  stayed green until the test was parameterised.
- **They resolve eagerly, with `ValidateOnBuild` off.** `AddExceptionHandler<T>()` registers a
  lazily-constructed singleton, so graph validation inspects the descriptor and never calls the
  constructor — which is how a bare-`string` dependency survived a green build. Validation also
  false-positives on MVC's own descriptors in a bare `ServiceCollection`. `ValidateScopes` stays
  on; it produces no such false positives and catches real captive dependencies.
- **A stored-procedure guard asserts on the migration source, not on SQL behaviour.** It stops
  someone deleting the guard; it does not prove the procedure runs correctly. Say which one you
  have, and get a live database for the other half.

## Adding a feature

1. Entity in the service's `Domain/Entities/` — with a `Rehydrate(...)` factory if its identity
   is database-assigned.
2. Repository interface in `Domain/Abstractions/`.
3. Concrete repository in `Infrastructure/Repositories/` — **execution only**.
4. `*CommandFactory` in `Infrastructure/Persistence/` holding that repository's
   `CommandDefinition` builders, reusing `SqlCommands` for the shared shapes.
5. DTO in `Application/DTOs/`.
6. Command or query in `Application/Commands/` or `Application/Queries/`.
7. Handler in the same directory — `IRequestHandler<TCommand, TResponse>`, or
   `IRequestHandler<TCommand>` for a void command.
8. FluentValidation validator in the same directory — with a case **at** each limit.
9. Controller in `Api/Controllers/`, delegating to `IMediator`.
10. Register in `Api/Program.cs`.
11. Route in `Services/ApiGateway/ocelot.json` under the `Routes` array (single source;
    `ocelot.Development.json` overrides locally).
12. UI pages if needed.
13. Unit tests in `Tests/UnitTests/<Assembly>/…`, mutation-verified.

## Pitfalls

Things that have cost time here, with the symptom they produce.

### Configuration and deployment

- **A stale image proves nothing.** `docker compose up` without `--build` reuses the previous
  image, so a green-looking start can be testing code that no longer exists. Rebuild before
  drawing a conclusion.
- **Missing `Initial Catalog`** → `DatabaseMigrationBase` fails with `CREATE DATABASE []`. The
  database name is taken from the connection string, so it is not optional.
- **`StartupTests` asserts `IGatewayApiClient` is a `Singleton`** because the BFF shares one
  `RestClient` and one token cache process-wide. If the lifetime ever changes, that is a
  deliberate decision, not an accident to "fix" in the test.
- **Editor tooling here is line-ending sensitive, and it is not only `patch`.** `patch` corrupts
  non-LF/UTF-8-BOM files (LF→CRLF, indent distortion); `replace_in_file` fails outright when its
  `old_text` uses `\n` and the file uses `\r\n`, and reports the misleading "text not found"
  rather than an encoding problem. Several files in this repository are mixed. Before a scripted
  multi-line edit, **count the line endings** and use explicit `[char]13` / `[char]10` or a
  line-indexed edit; then **grep for the inserted text**, because a `Replace()` that matched
  nothing still exits `0`. Finish with `dotnet build`.

### Runtime and correctness

- **A constructor on a DI-registered type takes interfaces, never primitives** — see
  [API and error contract](#api-and-error-contract).
- **TVP streams must not be buffered.** `AsSqlDataRecords()` reuses a single `SqlDataRecord`,
  so every yielded element is the _same_ object. Materialising the sequence (`ToList()`) returns
  the right number of rows all holding the last entity's values, and raises nothing. Enumerate it
  lazily, which is what `AsTableValuedParameter` does.
- **`$` is not an end-of-string anchor in .NET regex** — it also matches before a trailing
  `\n`. Use `\A…\z` for identifier validation. See [Persistence](#persistence).
- **A set-based batch UPDATE reports a count, not an identity** — see
  [Persistence](#persistence).
- **`ErrorResult` redaction is an allowlist** — see
  [API and error contract](#api-and-error-contract).
- **`NotificationRepository` hydrates by name and fails silently on a typo** — see
  [Persistence](#persistence).

### Test infrastructure

- **Architecture tests require `-c Debug`.** ArchUnit analyses IL, which only a Debug build
  produces. A Release run still discovers them and passes, so a green Release run does not mean
  anything was actually inspected.
- **ArchUnitNET's `Check` extension needs `using ArchUnitNET.TUnit;`**, not just
  `ArchUnitNET.Fluent`, or `IArchRule.Check()` does not compile. Package:
  `TngTech.ArchUnitNET.TUnit`.
- **`Times.Once` is a property, not `Times.Once()`.** `VerifyLog()` / `VerifyNoLog()` are
  extension methods in `TUnit.Mocks.Logging`, so the `using` is required even though
  `Mock.Logger<T>()` resolves without it.

## Related

- [Architecture](architecture.md) — component diagram, tech stack, project layout
- [Getting Started](getting-started.md) — build, secrets, ports
- [API Reference](api-reference.md) — endpoints, error contract, security model
- [Database](database.md) — schema, resets, stored procedures
- [Messaging & Dispatch](messaging.md) — licensing, the on/off switch, post-commit dispatch
- [Development](testing.md) — test projects and worked examples
- [Operations](operations.md) — CI/CD and the compose test environment
