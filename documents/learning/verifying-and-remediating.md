# Verifying and Remediating

[Back to Learning](README.md) · [Verifying a Result Honestly](verification-honesty.md)

The remediation plan for review 07 was executed and then audited against what actually happened. Its
most useful content is not the fixes — it is the places **the plan itself was wrong**, and the
verification habits that caught them.

## A sweep written as a task beats a sweep performed during analysis

The review asserted at **0.99 confidence** that `TestDoubles/Stubs/SyncHasher.cs` had zero call sites.
It had a live one, at `UserRepositoryTests.cs:40`. Deleting it as written would have broken the build.

This was the **third** time in that plan's history that a stated fact turned out to be wrong at the
moment of application: F-01's severity, the `AsyncLocal` flow claim, and now this.

> **A sweep that is written down as a mandatory task is worth more than a sweep performed during
> analysis.** Analysis-time confidence decays; a re-runnable check does not. The plan made the sweep
> mandatory and that is the only reason the build survived.

Corollary: a finding carrying a high confidence score is still a hypothesis **until re-verified at the
point of application**, and the confidence number does not transfer to a different moment.

## "The tool is not available" is a claim that must be earned

The compose gate was reported unverifiable on the strength of one `docker: not recognized` from the
Windows shell — and recorded as a **permanent limitation**. Docker 29.8.1 was reachable the whole time
through WSL, via the same route the original review had used.

The cost was not only a stale caveat. **The first real run immediately found a genuine defect** — a
hardcoded `Messaging__Enabled=true` that made the stopgap unusable — which no amount of YAML parsing
would have caught.

> Before declaring a capability unavailable, exhaust the alternative routes: another shell, a
> container, a different toolchain. Report a limitation as *earned*, and say what you tried.

## Check the tool, then check the tool's *state*

The first `compose up` returned **"Started"** and proved nothing, because the image was 18 hours old
and did not contain the code under test. A green-looking result from a stale artifact is the same
false-negative class as F-01 and R-23.

> **Rebuild before drawing a conclusion from a running container.** `docker compose up` without
> `--build` reuses the previous image.

## Prove a configuration is *read*, even when you cannot prove it *works*

A test could not demonstrate that a valid licence key is accepted. It could, however, show the error
changing from *"License must be specified"* to a *Base-64 parse error* — which proves the path is
honoured.

> A partial proof of **wiring** is a real, non-vacuous assertion. "The YAML parses" is not.
> Prefer the strongest claim you can actually establish, and state precisely which claim it is.

## A plan instruction and its caveat can pull in opposite directions

One task said to copy the `UI.csproj` `InternalsVisibleTo` pattern *verbatim*, and also to verify
`GenerateAssemblyInfo` first. The verification was load-bearing and came out **negative**:
`UserService.Infrastructure.csproj` sets `GenerateAssemblyInfo=false`, which disables the targets that
turn the `<InternalsVisibleTo Include="..." />` **item** form into an attribute.

Copying verbatim would have **compiled, produced no error, and left the member inaccessible** - a
silent no-op that reads as success.

> When a step says "do X" and "first check Y", **Y is the instruction.** A copy-verbatim step with a
> precondition attached fails silently when the precondition fails. Record the reason at the point of
> use so the next reader does not "correct" it back.

## The mutation gate earns its keep by finding more than expected

The redaction-gate remediation reverted to the fail-open form and produced **6 failures, not 1**. The
extra five came from argument-driven cases (`"Staging"`, `"Prod"`, `""`, `"   "`) the plan had not
asked for - added precisely because a denylist would have accepted all four.

> When a mutation fails more tests than expected, the surplus is usually the cases you did not think
> to enumerate. That is a signal to add more, not to be satisfied.

## Validation that is wrong in both directions

`ValidateOnBuild = true` **missed** F-01 entirely (the singleton is lazily constructed) **and**
false-positived on the MVC graph, where descriptors depend on services `WebApplicationBuilder`
supplies and a bare `ServiceCollection` does not. It was turned **off**, with the two-part rationale
written into the XML remarks so nobody restores it. `ValidateScopes = true` stays on - it produces no
such false positives and catches a real defect class.

> A validation setting can be simultaneously insufficient and noisy. Turning it off is a decision that
> must be justified **and documented**, or it silently returns.

Two more API shapes that bite when testing DI:

- `AddCheck<T>(name)` registers an **instance**, not a type. Resolving `T` throws. Assert through
  `IOptions<HealthCheckServiceOptions>` instead - which additionally proves the *name* survived.
- `IWebHostEnvironment` must be registered explicitly in a bare `ServiceCollection`, or the test fails
  for a reason unrelated to the defect under test and the real signal is buried.

## Verify removal repo-wide, not per project

Four `TUnit` package references were flagged unused. One (`TUnit.AspNetCore`) was flagged
"inconclusive" from a name sweep and **was in fact used** - found only by searching repo-wide for
`WebApplicationFactory`. The other three had zero hits and were removed from six projects.

> **A clean build in one project proves nothing about the other five.** Removal claims are repo-wide
> claims and must be verified that way.

Adding a `FrameworkReference Microsoft.AspNetCore.App` to a test project silently made three
`Microsoft.Extensions.*` `PackageReference`s redundant, producing 6 new `NU1510` warnings and breaking
the 0W/0E gate.

## Report what did not happen, and label partial verification honestly

- A task predicted new nullable diagnostics in two projects. **None appeared.** Recorded as observed,
  rather than letting the prediction stand as if it were a result.
- A remediation was recorded as **applied but explicitly not fully verified**, with the residual gap
  named.

> If a predicted effect does not occur, say so. And label partial verification as partial - that is
> what makes the remainder trackable.

## Default a switch to the safer failure

`Messaging:Enabled` defaults **ON**: absent, malformed, and blank all mean enabled. Defaulting the
other way would **silently disable the broker** in a deployment that merely forgot to set the flag.

> When a boolean flag decides whether a subsystem runs, default it to the state that
> **fails loudly**. Silence is the dangerous outcome, not absence.

`NullDomainEventDispatcher` was likewise judged **required, not optional**: skipping the bus leaves
`IDomainEventDispatcher` unresolvable and breaks the post-commit dispatch path entirely. Dropping
events - loudly logged - beats a dead pipeline.

## A red suite is not evidence until you know why

After mutation-verifying three guards, the first "final" full run reported exactly those three tests
failing. Cause: `dotnet test` was launched in the same tool call as the restore commands, and the run
**raced the file restores** - the restore had not flushed when the test process enumerated sources.

> After any mutate/restore cycle, **verify the restore by reading the file** (grep for the marker), then
> rebuild non-incrementally, then run. A suite result that contradicts your own last action is
> information about the harness, not about the code.

> **A red suite proves nothing until you know why.** The inverse of "a green suite proves nothing",
> and just as important. Three failures that match your last three edits is a tooling race; three
> failures that match nothing you touched is a real defect. Distinguish them before reacting.

The correct decomposition after a failed attempt: `dotnet build --no-incremental` then
`dotnet test --no-build`. `dotnet test --no-incremental` is not a valid flag and yields
`total: 0, error: 7` with nothing in either stream.

## A test-count delta is a ledger, not a number

The suite went 386 -> 390, recorded as a reconciliation rather than an observation:
**-1** dead controller test replaced, **+1** messaging test parameterised (1 case to 2), **+1** SP
idempotency text guard, **+3** NotificationService tests.

> When a count moves, **account for every unit.** A net number is unfalsifiable; a signed list is
> checkable, and it forces you to notice a test you deleted without replacing.

## A text guard is honest only if it says so

The N-07 guard asserts against the *migration source text*. It stops a deletion regression; it does
**not** prove the stored procedure behaves correctly against a live TVP insert.

> Know which of two things you have: a **behavioural** guard (executes the thing) or a
> **textual** guard (proves the definition is present). A textual guard is genuinely useful - it is the
> only kind possible without a live database - but the moment it is described as proving behaviour, the
> gap becomes invisible. The README now says "pinned as text only".

The closing check was named precisely: against a disposable database, run the procedure twice with the
same `(NotificationId, UserId)` and confirm exactly one row plus success.

## Mutation against production code, not against the assertion

Three guards were mutation-verified. Two were proved by **breaking the implementation** - deleting
the `else` branch in NotificationService DI registration, replacing the clamped `Take(affected)`
with a bare `toSend`.

The N-11 mutation is the informative one: it failed **only** the NotificationService case, which is
exactly the gap revision 1 had missed. The mutation reproduced the original defect signature.

> A mutation that edits the test proves only that the test reads its own string. A mutation that edits
> the production code proves the guard was load-bearing. Prefer the second, always.

A control case was also written in the opposite direction - a test proving the trim drops nothing when
every row is affected - so the fix cannot over-correct into silent event loss. It was not separately
mutated, because it is the control for a code path the red mutation already proved reachable.

## Count your own predictions against the arithmetic

Recorded outcomes were checked against what the plan predicted, and the mismatches were written down
rather than smoothed over:

- Planned a regression test that does not mock the repository; shipped a text guard instead. Correct,
  and recorded as a deviation.
- Planned "log/flag the discrepancy" for N-10; the handler had **no logger**, so the task needed a new
  constructor dependency the plan did not anticipate.
- Planned a documentation correction to the idempotency claim in `AGENTS.md`; grep returned
  nothing, because the inaccurate sentence was in section 5.3 of the review report, not there.

> The first instance also shows why grepping before editing matters: the plan asserted a sentence
> existed, the grep proved it did not, and the correction went to the right file. A plan that had been
> trusted would have produced an edit to a file with nothing to change.

## Not everything needs the maximal fix

N-09 offered three options: document a pre-step, `create_host_path: false`, or a compose profile. Two
were applied and the profile was rejected with a reason: a profile changes what `up` starts, which is
the one thing someone debugging a licence error does not want to debug simultaneously.

> When choosing among correct options, prefer the one with the **smallest blast radius on the failure
> path you are trying to make legible**. A workaround should not introduce a new thing to debug.

The plan also declined `OUTPUT INSERTED.Id` as disproportionate while the event has no consumer, and
said so in a code comment rather than leaving the gap implicit.
