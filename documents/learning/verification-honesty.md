# Verifying a Result Honestly

[Back to Learning](README.md) · [Testing](../testing.md)

A verification that cannot be reproduced by the person reading it is not a verification. This file
records three ways that failed here, all of which produced *confident, precise, wrong* output.

## 1. A "green" reported from a doctored environment

**What happened.** The broker-backed suite was reported as `2/2 passed`. It was true — inside my
shell, where I had run this before every test:

```powershell
Get-Content .env | ForEach-Object { ... SetEnvironmentVariable($matches[1], $matches[2], 'Process') }
```

The user's shell never had those variables. Their run failed immediately with `ACCESS_REFUSED`.
The suite was not green in the environment anyone else would use.

**Root cause.** I verified a configuration the user does not run, then reported the result as if
it described the project. The verification step itself mutated the ambient state.

**The rule.**

> A "green" claim is only valid for the command the user will actually run, in an environment they
> will actually have. If a verification step mutates ambient state, the result describes *that*
> state — verify in a clean environment, or state the precondition next to the result.

**Concretely:** open a shell with nothing exported and run the plain command. That is the only
environment that counts.

## 2. The outer exception lied; the inner one did not

**What happened.** MassTransit reported:

```
RabbitMqConnectionException: Broker unreachable: guest@127.0.0.1:5673
  --> RabbitMQ.Client.Exceptions.AuthenticationFailureException: ACCESS_REFUSED
```

An **authentication rejection** presented as a **reachability failure**. Several rounds of the
investigation were spent on the transport, the topology, and stale build output before the inner
exception was read. The real cause was that `docker compose` reads `.env` automatically and
`dotnet test` does not, so the container ran with the real account while the test process fell back
to `guest` — which RabbitMQ refuses off-loopback.

**The rule.** Read the inner exception before blaming a component. When a wrapper reports "unreachable",
check whether it means *no listener* or *connection refused*; those have opposite fixes.

## 3. A probe that could only answer half the question

`EnsureReachable()` opened a TCP socket. It therefore could not detect an authentication failure,
yet it was the gate that decided the broker was "reachable". A reachable broker and a *usable*
account are different properties, and the probe only tested the first.

**The rule.** A diagnostic that can report the wrong component must say so in its own output.
The message now states plainly that the probe is TCP-only, cannot see auth failures, and that
`ACCESS_REFUSED` means the account was rejected.

## 4. Calibrate the instrument before believing it

The same failure mode, one level down, appeared three times:

| Instrument | Why it misled |
|---|---|
| A broker counter read on a **warm** broker | `message_stats` is cumulative and interval-aggregated; the delta described prior runs |
| A test run in a **doctored** shell | The result described the shell, not the project |
| Document contents **recalled** from an earlier session | The claim was never checked against the file |

**The rule.** Before trusting a measurement, confirm it would have caught the thing you are looking
for. A counter on a warm broker, a suite in a doctored shell, and a file you did not open cannot
produce a false negative — which is exactly why they produced false positives here.

## 5. A guard is only worth what its mutation proves

A passing test proves nothing until it has been made to fail for the right reason. The
broker-backed guard's acceptance criterion was: **remove the `(object)` cast, confirm red, restore,
confirm green.** Until that ran, the guard was an unverified claim that it would catch the defect.

After any change to a guard itself, re-run the mutation. A green that cannot be made red is not a
guard, and the change may have weakened it.

## 6. An incremental build reported "0 warnings" and it was an artifact

Twice, an incremental `dotnet build` reported `0 Warning(s)`; a later full compile reported 2, then
10. The zero came from **up-to-date projects being skipped** - not from a clean compile.

> **Claim 0W/0E only from a cold build:** `dotnet clean` then `dotnet build`.

The same shape as every other entry here: a precise, confident number that measured the tool's
shortcut rather than the code.

## 7. A green suite is not evidence of correctness

This repo once carried **106 passing tests with empty bodies**. They were discovered, ran, and were
reported green. No amount of suite-level green says anything about whether a test asserts anything.

See [Vacuous and Self-Referential Tests](vacuous-and-self-referential-tests.md) for the three shapes
and the mutation table. The short form: **a new or rewritten test must be shown capable of failing.**

## 8. Scope a repository-wide grep, or do not run it

The first sweep for `FluentAssertions|NUnit|Moq` reported **11 matches** - every one in jQuery,
Bootstrap, `README.md` or `AGENTS.md`. Re-scoped to `Tests/UnitTests/**` it returned **0**.

A raw repo-wide grep produces false positives that look alarming and are **worse than no check at
all**, because they train you to ignore the output.

Related: verify success criteria **by counting matches, not by asserting them**. A criterion backed
by a count is auditable; one backed by a claim is not.

## 9. A reverted mutation that never reverted

A PowerShell parse error aborted a command *before anything ran*. The mutation and its revert shared
that one command, so **the revert never executed** - leaving a live mutation in production that the
next green suite happily reported.

> **Never chain a mutation and its revert into a single command.** Verify a revert by **re-reading
> the source**, never by trusting the following test run.

This is the most dangerous entry in this file, because the test run is exactly the evidence a person
would accept.

## 10. Instrument before refactoring

A plan proposed replacing the `AsyncLocal` collector with a scoped buffer. Two log lines and one
request (~15 minutes) returned handler `1`, pipeline `1` - **disproving the diagnosis outright**. The
refactor would have been clean, well-argued, and changed nothing observable.

> When a diagnosis names a component, **observe that component before changing it.** A refactor built
> on a wrong diagnosis is more expensive than the bug, because it looks like progress.

## 11. A green suite does not detect a deleted test

Removing temporary logging scaffolding deleted an adjacent test method. The suite went **389/389** and
stayed green. Only a `[Test]`-attribute census against `HEAD` caught it.

> Passing green proves the remaining tests still pass. It says nothing about **how many there are**.
> After cleanup work, count the tests, not just their result.

## 12. Some guarantees are only observable at runtime

`POST /Notifications` returned 500 on every success. The unit test constructed the controller, called
the action, and asserted on `CreatedAtActionResult.ActionName` - **and passed**. The failure occurred in
result *formatting*: `SuppressAsyncSuffixInActionNames` defaults to true since ASP.NET Core 3.0, so
the selector is `GetById`, not `GetByIdAsync`, and `OnFormatting` needs a real `ActionContext` that a
unit test does not supply.

> When a guarantee depends on a framework convention resolved at request time, a constructed object
> cannot test it. Assert the *declared* thing (route name, values) and leave the runtime resolution to
> an integration test.

## 13. Elimination has a stopping point

Every structural explanation was eliminated, and the correct next move was to **observe** - but three
more rounds of reading source happened first. Elimination is not progress toward a cause; it is the
removal of things that are not the cause.

> When elimination is complete, the next step is instrumentation, not more reading. The only
> unobserved fact is usually the one that matters: *is the method ever entered?*

The instrumentation that answered it was four properties on the consumer - `ConsumeEntered`,
`LastObservedMessageType`, `LastObservedMessageId`, `LastObservedBody` - and it split the remaining
space cleanly:

| Observation | Conclusion |
|---|---|
| `Ready` fires, `PreReceive` never | transport never delivers; fault is the AMQP consumer path |
| `PreReceive` fires, `Consume` does not | fault is in the receive pipeline between transport and consumer |
| neither fires | endpoint never reached ready, despite `consumers=1` on the broker |

## 14. Check for orphans before blaming the environment

A "stale binding / more than one bound queue" condition persisted across several rounds and read as
framework behaviour. The cause was a **stray test process (PID 8444)** still holding its own queue on
the shared publish exchange. Because the publish exchange is fanout, messages went to both queues.

> Before attributing a signal to the environment, check for orphaned local processes. The signal was
> real; its cause was local.

## 15. Decouple a proven fix from an unfinished guard

The production defect was root-caused, fixed, and proven live in the first working session.
Everything after that went into a test harness that was **itself** defective four separate times, and
which then blocked the fix from landing.

> A proven fix should be landable on its own merits. An incomplete guard must not hold a verified
> production change hostage - and committing a red guard alongside a green fix trades a real
> guarantee for a nominal one.

## 16. Write the rule down, then check you are following it

"Failed lookup is not absence" was recorded after one incident and violated immediately after, costing
an hour. Later, a superseded inference from an earlier revision was still doing duty in newer framing
until the evidence was re-derived.

> Two rules: **re-read your own notes before repeating a mistake**, and **when you withdraw a claim,
> propagate the withdrawal** to every place it is still load-bearing.

## Re-running an inherited result, and admitting when you did not

The first pass of report 08 took the test result from session CONTEXT, a prior turn claim and not
its own execution. Revision 2 re-ran both gates and said so explicitly in the header, marking them
as re-run and verified this pass, and noting that revision 1 relied on session CONTEXT.

> **A result carried forward from context is a citation, not a measurement.** Re-run the gate whose
> number you are about to publish, and state which pass produced it. A single qualifier - re-run this
> pass - separates a verified number from a remembered one.

## Publish the limit next to the claim

Every finding carried a confidence figure, and the reasons for those figures were specific rather than
decorative: 0.88 for a three-file reasoning chain never executed against SQL Server; 0.97 for a
two-file contradiction read directly; 0.80 for a prediction about Docker bind-mount semantics not
reproduced live.

> **Confidence is only meaningful next to what produced it.** `0.88` alone is decoration.
> No SQL Server was ever started is the finding. The stated limits were: not mutation-tested

Two things this buys. A reader can decide which claims to re-verify. And a future pass can *shrink*
the uncertainty rather than repeat it — which is what revision 2 did, by re-running the gates.

## A re-review that finds little is not a failed re-review

Report 08 reviewed an already-vetted change set, and said so before reporting the result: a thin
finding set was the expected and correct outcome, not a sign of a shallow pass. It then found
five real ones anyway.

> When the expected yield is low, **say the expectation first** and then report what you actually
> found. Otherwise a small result reads as a shallow review, and a reader who assumes that may stop
> looking. The framing is what makes a negative result trustworthy.
