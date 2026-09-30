# Testing Seams

[Back to Learning](README.md) · [Conventions](../conventions.md)

Some code cannot be intercepted by a mock. The answer is **not** a second mocking library and not a
weaker assertion — it is to extract the un-testable part into something pure and test *that*.

## The pattern

> When a dependency cannot be intercepted, move the un-testable part into a pure, public unit and
> test it directly. A seam is justified when the extracted part is **pure** and **carries the risk**.
> Do not extract merely to make a test compile.

Applied twice here, for two unrelated blockers:

| Seam | Before | After |
|---|---|---|
| MassTransit consumer | `Consume(ConsumeContext<UserRegisteredEvent>)` — unmockable `ConsumeContext<T>` | `Consume(ctx)` → null-check → `Handle(message)`; the **logic** moves to `public void Handle(UserRegisteredEvent)` |
| Dapper repository | `QuerySingleOrDefaultAsync` / `ExecuteAsync` called directly on a connection mock | SQL and parameters move to a `Persistence/*CommandFactory` of pure `CommandDefinition` builders; the repository **only executes** |

In both cases the extracted unit is a function of its inputs with no I/O, and it is where the real
defects live — the log message, the SQL text, the stored-procedure parameter names.

## Dapper is not mockable — the specific reasons

- **`QuerySingleOrDefaultAsync<T>()` and `ExecuteAsync()` are static extension methods on
  `IDbConnection`**, not interface members. A source-generated connection mock has no member to
  intercept, so a test written as if it did **passes vacuously**.
- **`DbCommand.Mock()` generates but exposes neither `CreateParameter()` nor `Parameters`** — both are
  `protected`. Loose mode yields a `null` collection and Dapper throws `NullReferenceException` at
  `CommandDefinition.SetupCommand`. Do not spend time on this path again.
- **Dapper casts the ambient transaction** to a `DbTransaction`, so an `IDbTransaction` mock throws
  `InvalidCastException` at `set_Transaction`. Any ambient-transaction double must be a real
  `DbTransaction` subclass.
- **`DynamicParameters` is not enumerable** and its `ParamInfo` is `internal`. Only `ParameterNames`
  is public; values come from `Get<T>(name)`. Casting to `IEnumerable` throws.
- **`ParameterNames` omits the `@` prefix** while `Get<T>` accepts both. Normalise in the helper.
- **`DbType` and `Size` are not publicly observable** — asserting them needs reflection into a
  third-party internal type. Assert **names and values** instead, which is where the regression risk
  actually is.
- **`Get<T>` throws for a parameter that was never assigned** — it does not return `default`. So a
  guard like `if (!parameters.Get<bool>("@SPSuccess"))` is unreachable without a live provider.
  Assert the throwing behaviour and say why, rather than pretending the branch is covered.
- **An anonymous projection's property names ARE the wire parameter names.** A parameter-name helper
  must handle four shapes: `null`, `DynamicParameters`, `DbParameterCollection`, and anonymous —
  all normalised to a leading `@`. Getting this wrong is a real `SqlException 8144` at runtime.

## State the seam's limits in the test file

`UserRepositoryTests` documents in its XML remarks that row mapping and the `_connection.XxxAsync(cmd)`
call remain uncovered. **An honest boundary is worth more than an implied one** — a reader who knows
what is untested can judge the rest.

## Source-scanning contract tests

`Tests/WiringTests` asserts stored-procedure contracts by reading **production source text**. Two rules
make these durable:

- **Scan a *project*, not a *file*.** A guard that read `Repositories/UserRepository.cs` by literal
  path broke the moment SQL moved to `Persistence/UserCommandFactory.cs` — a regression introduced
  during the work and missed by per-project verification. The guarded property is *"every bound
  `@Parameter` matches an SP header"*, not *"this string lives in this file"*. **Any test that pins a
  file path to assert on content is a latent break.**
- **A gate that runs only some test projects is not a solution gate.** `WiringTests` is in no
  success criterion and was simply missed until a regression got through.

**Repairing a test means proving it still bites.** After changing what a test scans, re-run the
mutation against the new scope. Changing a test's *scope* is exactly where a "fix" can quietly gut it.

## The seam that was widened for a test and broke production

To test the exception handler, its constructor was changed from `IHostEnvironment` to a bare `string`
environment name. It compiled, resolved, and shipped - then threw only when the error path first needed
it, and nothing in the container could construct it.

> **Widening a production type to make it testable is a design change, not a test change.** If the type
> needs an untyped `string` to be testable, the test is asking the production design a question it
> cannot answer. The correct fix is a test double for the interface (`TestHostEnvironment`), not a
> narrower production signature.

The mirror-image guidance from the same review, which is worth stating as the general rule:

> **Prefer an interface boundary the test can substitute over a visibility change in production.**
> `InternalsVisibleTo` plus an `internal` member is a seam that costs no public surface. Widening a
> constructor parameter type costs correctness.

## Source-scanning contract tests, and their honest limit

Dapper's `QuerySingleOrDefaultAsync` and `ExecuteAsync` are static extension methods on
`IDbConnection`, so a test double on the connection cannot intercept them. The only observable unit is
the `CommandDefinition` the `*CommandFactory` builds.

> **When a dependency's API is not virtual or not an interface member, the observable unit moves up to
> whatever you construct for it.** Extracting SQL into factories is what made the write path testable at
> all - and it is a legitimate production design in its own right, since it keeps repositories to
> execution only.

The limit is stated in the test file rather than implied away: the guard reads the SP text and pins its
parameters and guards, but **row mapping and the Dapper execute call are not unit-testable** - they
need a live ADO.NET provider.
