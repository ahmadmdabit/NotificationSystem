# Broken Diagnostics

[Back to Learning](README.md) · [Verifying a Result Honestly](verification-honesty.md)

A diagnostic that is silently wrong is worse than no diagnostic: it produces a confident, specific,
false message that redirects the investigation. In this project **five separate diagnostics** did
that, all with the same shape, and the pattern itself is the lesson.

## The shape

> The instrument reports **absence**, the reader interprets it as a fact about **the system**, when it
> is a fact about **the query**.

Every instance below fits. If yours does not, this file may not apply.

| # | Diagnostic said | Actually meant | Cause |
|---|---|---|---|
| 1 | "`Publish` was never invoked" (stated at 0.98 confidence) | I queried **3 of 5** exchanges; the base-type exchange carried every publish and was never sampled | partial enumeration |
| 2 | "No queues bound to the publish exchange" (`NONE`) on a healthy bus with `consumers=1` visible | The first hop targets an **endpoint exchange**, not a queue — the hop after that was never traversed | wrong topology model |
| 3 | "No bound queues" from a bindings walk | Management API field is `destination_type`; I read `destination_kind` (what `rabbitmqctl` prints). The filter rejected every row | wrong field name |
| 4 | "More than one endpoint exchange bound" — failed on any previously-used broker | Deleting a queue does **not** delete its exchange; every historical run leaves one | assertion too strict |
| 5 | `publish_in` on the base exchange proved erasure | MassTransit's **inheritance binding** forwards a copy, so a *correct* publish increments it too | wrong proxy for the property |

**The corrective reflex:** when an assertion or probe reports nothing, *first ask whether the
instrument is wrong* before believing the message. Its failure text will confidently describe the
broker, the network, or the environment — the three places a reader will not look.

## Absence of evidence requires enumerating the whole space

Error 1 is the purest form. The premises were **both true and correctly cited**: `message_stats` was
absent, and the RabbitMQ maintainer had stated those counters are omitted when there has been no
publishing activity. The conclusion was wrong anyway, because the query was partial.

> **Absence of broker traffic is only proof once every exchange has been enumerated.** The answer was
> in the objects never queried.

This is the same shape as Dapper's absent nullable diagnostics, already logged in this repo: a
settings-level switch silently changing a diagnostic, producing a green build that hid a live defect.

## An unconfigured feature leaves no trace

A missing `_error` queue was read as proof that no message had failed. Wrong in both directions:
`RabbitMqErrorSettings` declares its queues **only if configured**, and the default endpoint
configures none. Absence is consistent with both success and total failure.

> **Before inferring from something that does not exist, confirm the thing would exist if it had
  happened.**

## Assert on the property, not a proxy

`publish_in` *looked* like a routing signal and was not. The distinguishers are subtle:

- `publish_in` — messages published **to** the exchange, regardless of routing; also incremented by
  inheritance forwarding.
- `publish_out` — messages actually routed onward.

When a proxy is chosen, ask what else moves the same number. Better still, assert the property
directly: `IBusTopology.TryGetPublishAddress(Type, out Uri)` returns the exact value the defect got
wrong, with no publication, so it **cannot be timing-dependent**.

## Assert only what must be true

`AssertSingleEndpointBindingAsync` required this run's endpoint exchange to be the *only* one bound.
That could never pass twice on the same broker. Replaced by an assertion on the property that matters
— *this run's queue is reachable through both hops* — with stale exchanges **reported in the failure
text** rather than asserted against.

> Assert the invariant. Report the noise. A strict assertion about ambient state is a time bomb.

## Confirm the API is public before designing against it

A planned in-process topology walk was **impossible**: `ConsumeTopology` and
`ReceiveEndpointTopology` are `internal`. Reflect over the pinned assembly first. Five minutes versus
a design that cannot compile — and the public substitute found here was strictly better.

## Overload resolution and framework decoys

- `Publish(object, Type, ...)` gives explicit type control; `Publish<T>(object values, ...)` is the
  DynamicProxy **dictionary** overload. Passing an anonymous object nearby silently selects a
  different path.
- A type named exactly like the one you want may be **saga-only** and unrelated.
- A type may live in a different assembly than expected — here `MassTransit.Transports.PublishEndpoint`
  was in `MassTransit.Abstractions.dll`, which is why five targeted lookups missed it.

## A process check that matched its own command line

`pgrep -fc "compose build"` reported `RUNNING` for a build that had already died, because the pattern
matched the polling command itself. The build was relaunched under `setsid` and verified by PID.

> **A liveness probe that greps process lists can match itself.** Verify by PID, or use a mechanism
> whose own invocation cannot satisfy the query.

## A stale image proves nothing

Container images pre-dating the change were 5 days old and could not have contained it. Verifying
against them would have "confirmed" anything.

> **Rebuild before drawing a conclusion from a running container.** `docker compose up` without
> `--build` reuses the previous image, so a green-looking start can be testing code that no longer
> exists.

## A test that never builds the thing it guards

Not a broken *query* but a broken *premise*: `WiringTests` covered the Application and Infrastructure
scopes and never touched the `.Api` projects, so a DI registration there could not fail. See
[Defects That Escape a Green Suite](defects-that-escape-green-suites.md).

> Before trusting a guard, confirm it **exercises the component it names**. A wiring test that skips
> the API project is a wiring test for a different system.

## A normative citation to an untracked file

Production code cites `AGENTS.md` normatively: `SqlCommands.cs` remarks, `TvpStreamingExtensions`
notes, the `NotificationRepository` pitfall. But `AGENTS.md` was untracked.

> **A rule that lives only in an untracked file is not a repository rule.** For the author it is
> load-bearing; for the next contributor it is a dangling reference, and the code it explains becomes
> unexplained. When documentation is cited from source, it belongs in the repository.

This is a *documentation* defect that reads as a *wiring* one: nothing fails, nothing is inconsistent,
and the knowledge simply is not there for the person who needs it.

## Related: the mirror image — instruments reporting presence

Every case above is an instrument reporting **absence** that was really a fact about the query. The
inverse is equally misleading and is documented in
[Green Instruments and Dead Services](green-instruments-and-dead-services.md): a container reported
`(healthy)`, `docker port` printed a mapping, and nothing was listening. Same corrective reflex —
name what the instrument measures before writing the sentence you intend to conclude from it.

## Related: an ignore file that does not match the working tree

`.gitignore` was split between the index and the worktree — staged content ended at `tempkey.rsa`,
while the worktree additionally ignored `.hermes/`, `.maxential/`, and `tmp/`. `.dockerignore` carried
the same three patterns, also unstaged.

> Committing as-is publishes an ignore file that does not describe the repository the developer is
> actually using. Nothing flags this: the build is fine, the tests are fine, and three local-artifact
> patterns quietly become trackable.

Two lessons in one shape: **a config file is part of the change set it belongs to**, and an ignore rule
that exists only on one machine is a personal convention, not a project one.

## The check that was trusted most and was wrong most

Three successive research passes converged on `message_stats.publish_in` as the ground truth for
"did the broker receive this message", and the third rated it **0.98** - higher than its confidence in
any hypothesis about the defect. It was wrong for two independent reasons: interval aggregation on a
200–500 ms cadence, and MassTransit inheritance bindings moving the base exchange counter even when
routing is correct.

> **Before trusting a diagnostic, construct a case where it returns the wrong answer.** For a counter
> that is both *delayed* and *not specific to the intended path*, that case is easy to build and the
> counter should have been demoted early. Two confidently wrong conclusions came from it first.

The same pattern appears in the TCP-only `EnsureReachable()` probe: it answers "is there a listener",
which is true during an `ACCESS_REFUSED` and therefore reports an auth rejection as a network failure.

> Name what the instrument actually measures, then compare it to what you are about to conclude from
> it. The gap between those two sentences is where every misdiagnosis in this repo originated.

## Three passes, three wrong causes, one right mechanism

The silent-loss investigation produced three documents. Each eliminated hypotheses correctly. None
found the cause. The real defect - generic type erasure producing an unbound exchange - was visible in
the static type at every stage and was never proposed.

> **A long chain of increasingly refined hypotheses is not progress toward the answer; it is progress
> away from the one thing you have not looked at.** When three passes agree on a mechanism and
> disagree on the cause, the untested variable is the one nobody has instrumented. Here it was the
> exchange name - observable in one log line, and absent from every pass.
