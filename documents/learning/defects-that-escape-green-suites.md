# Defects That Escape a Green Suite

[Back to Learning](README.md) · [Development](../testing.md) · [Verifying a Result Honestly](verification-honesty.md)

A 16-finding code review of a 157-file commit produced the most useful single result in this repo's
history, and it is a fact about the suite rather than about the code:

> **360/360 green, a clean 0W/0E Release build, and a successful container image build all coexisted
> with a service whose error handler could not be constructed by the DI container.**

Every one of these escaped for the same reason: the thing that would have caught it was never run.

## The flagship: a `string` constructor parameter

A test-rewrite commit changed a constructor parameter from an interface to a primitive:

```diff
-public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment)
+public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, string environmentName)
```

`AddExceptionHandler<T>()` registers `T`, and the container selects the greediest constructor it can
satisfy. `IHostEnvironment` is registered by the host. **`System.String` is not, and cannot be** — a
sweep of all 18 registrations in the solution returns none. Reproduced verbatim against the real type:

```
Unable to resolve service for type 'System.String' while attempting to activate 'ApiExceptionHandler'
```

**Why the suite was green:**

| Test | Why it missed it |
|---|---|
| `ApiExceptionHandlerTests` | Constructs the handler with `new ApiExceptionHandler(logger, "Development")` — never goes through a container |
| `WiringTests` | Builds only the Application + Infrastructure scopes; never calls `AddExceptionHandler<T>()`, never touches the `.Api` projects |
| `EndpointSmokeTests` | Its own XML remarks say it *"deliberately does not boot the host"* |

> **A single missing registration escapes the entire suite when nothing in the suite ever builds the
> API host's service collection.** A wiring test that does not wire the thing cannot catch it being
> unwired.

**The motivation was reasonable and the fix was wrong.** It was changed so a unit test could pass a
literal string instead of stubbing `IHostEnvironment` — trading a small test double for a
production-wiring break. **When a test convenience requires changing production signatures, that is
the finding, not a detail.**

## The failure was worse than a startup crash

A probe established *when* the failure fires:

| Step | Outcome |
|---|---|
| `AddExceptionHandler<T>()` + `Build()` | **succeeds** - the singleton is not constructed |
| `MapHealthChecks("/health")` | **succeeds** - does not force resolution |
| `app.UseExceptionHandler()` + map | **succeeds** - still not resolved |
| `GetRequiredService<IExceptionHandler>()` | **fails** with the `System.String` error |

The container starts and stays healthy. The failure fires **the first time the handler is actually
used** - the error path, where the entire purpose is returning a well-formed `ApiResult`. A startup
crash is obvious; this is a silent trap on the one path nobody tests by hand.

## `ValidateOnBuild` does not cover `AddExceptionHandler<T>`

The direct consequence, and a general trap: `ValidateOnBuild` validates the service graph, but this
singleton is **lazily constructed**, so a broken constructor slips through. The guard must
*actively resolve* it:

```csharp
var handler = scope.ServiceProvider.GetRequiredService<IExceptionHandler>();
await Assert.That(handler).IsNotNull().Because("ApiExceptionHandler must be DI-constructible");
```

> **A validation that inspects a descriptor is not the same as one that constructs the service.**
> Where a DI registration is lazy, the guard must be an eager resolve - otherwise the proposed test
> passes green against the broken code. This is the same false-negative class the review had already
> found twice.

## Injected mocks that are never verified

Two logger mocks were created and then never inspected - the tests asserted only the return value and
the rethrown exception. Consequence: `LoggingBehavior.Handle` could **delete both** its
`LogInformation` calls and the suite would stay green. The severity split between `LogError` (500) and
`LogWarning` (4xx) - a real behavioural distinction - was asserted by nobody.

> **Verify every mock you inject.** `WasCalled(Times.Once)` is the point of using a mocking library; a
> stub-only test does not prove the dependency was used.

## Guards that never build the thing they guard

| Gap | Effect |
|---|---|
| `WiringTests` skipped the `.Api` projects | The DI break was invisible |
| `ApiExceptionHandlerTests` bypassed the container | Same |
| `NotificationRepository` still hydrated through the non-public Dapper constructor **after** `Rehydrate` was added | The mitigation sits in the codebase, unused, while the risk is live on four paths |
| README documented a coverage workflow the test platform ignores | A claim nobody could verify |
| README described `ArchitectureTests` as NUnit in three places, in the same file rewritten to say TUnit | Documentation drift inside a single commit |

> **A mitigation that is added but never adopted is worse than none**, because it is read as a
> resolved risk. Follow a new safety mechanism through to its call sites, or say explicitly that it
> is unused.

## Inconsistency within one commit is its own finding

The same commit added a test seam as `internal` + `InternalsVisibleTo` on the UI side - correct,
documented, no public API change - and eleven files later added the **same kind** of seam as `public`
on the messaging side. The XML remark justified the *extraction* of the method, not its *visibility*.

> When two files in one commit solve the same problem differently, a future reader cannot tell which
> convention is the project's. The inconsistency costs more than either choice.

## Pre-existing defects mask new ones

`docker-compose` could not start either service (MassTransit licence gate). F-01 would have been masked
by that crash in any compose smoke test - it only surfaced once the bus was bypassed.

> Track a blocker on the verification path as its own finding. If the documented manual check cannot
> run, say so - it should not be discovered for the first time during a release.

## Review method that made this findable

- **State explicit limits on confidence up front** - no host boot, no database, mutation checks not
  re-verified - and mark plan claims as *Observed*, not *Verified*.
- **Give every finding a confidence score and a file:line.** A reader can then triage by expected
  value.
- **Separate what was inspected by which method** (full diff reads, per-file `numstat`, cold build,
  reference-graph sweep) so the reader knows what a green statement actually covers.
- **Sweep before concluding** - 18 registrations checked, not 2.
- **Distinguish "reproducible now" from "inferred from code path"**, and upgrade the claim only after
  a probe - explicitly noting the correction when the probe changes it.
- **Report process failures too.** A `pgrep -fc "compose build"` that matched its own command line and
  reported `RUNNING` for a build that had already died is recorded so the transcript is not mistaken for
  a clean first attempt. See [Broken Diagnostics](broken-diagnostics.md).

## Asymmetric coverage: the same branch, guarded in one service and not the other

`Messaging_Disabled_ResolvesADispatcherAndRegistersNoBus` exercised **UserService only**.
`NotificationService.Infrastructure/DependencyInjection.cs` has an identical `if/else` with an
identical `NullDomainEventDispatcher` branch, and no equivalent test.

A regression that deleted the `else` — or registered **both** dispatchers — in NotificationService
would have left the entire suite green. The suite was large, the assertion was real, and the
guarantee was not.

> **Two services mean two code paths, and a test proves only the one it names.** Whenever a shape is
> duplicated across services, the guard must be duplicated with it. Where it is not, the duplicate is
> unverified by construction.

The fix is a parameterisation — `[Arguments(true, false)]` over both services — which is small,
test-only, and zero production risk. The generalisable rule: *coverage is a property of the pattern,
not of the instance.*

## A mock that hides the very thing under test

`Handle_WhenNotificationAlreadySent_DoesNotReEmitDomainEvent` mocks `INotificationHistoryRepository`.
It correctly proves the handler emits no second `NotificationSentEvent` — and is structurally
incapable of observing the repeat-insert PK violation that the same handler enables.

> When the defect lives in a **storage constraint**, a mocked repository test is not a weaker guard,
> it is the *wrong* guard. See [Natural Keys and Idempotency](natural-keys-and-idempotency.md).

## A dead branch, and the question it invites

`Handle` was `Task<bool>` but only ever returned `true` (or threw), making the controller's
`else BadRequest(...)` unreachable. The finding was deferred as vestigial rather than fixed — then
fixed later, on the grounds that a signature which cannot report failure is a lie in the contract.

> "It is harmless today" is a statement about the **present**, not the signature. An unreachable
> `else` is a small, cheap removal; the cost is that it advertises a failure mode the code cannot
> produce.

## Risk by hazard, not by pattern

`CREATE DATABASE` interpolated `databaseName` into a single-quoted literal with no quote escaping —
a textbook injection shape, deferred. The reasoning that made deferring defensible: the value derives
from an operator-supplied connection string, and that operator already holds DB-admin credentials in
the same string. Marginal exploit value.

> Rank injection by **who controls the input and what they already hold**, not by matching a pattern.
> A reachable-but-harmless sink outranks an unreachable one, and a string concat is not a finding until
> someone can influence it. Record the reason next to the code so the next reader does not "fix" it.
