# Confident Analysis That Was Wrong

[Back to Learning](README.md) · [MassTransit Publish Routing](masstransit-publish-routing.md) · [Broken Diagnostics](broken-diagnostics.md)

An early analysis of the silent domain-event loss ranked five hypotheses with explicit confidence
scores, quoted maintainer statements, and recommended a step-by-step fix. The reasoning was careful and
the primary conclusion was **wrong**. The actual defect was simpler and had been sitting in plain sight.

## The scorecard

| Hypothesis | Confidence assigned | Outcome |
|---|---|---|
| Missing receive endpoint / pure publisher | **0.75** | Real situation, wrong cause |
| `UserRegisteredEvent` published but not consumed | 0.60 | Wrong |
| v8/v9 `Publish`/`Send` semantics changed | 0.15 | Correctly downweighted |
| `AddTransactionalBus` silent drop | 0.25 | Not present |

> **Confidence assigned to a cause is a claim about a mechanism you have not yet exercised.** A 0.75
> is not "mostly right" — it is a statement that the evidence did not distinguish between two
> explanations. Recording the number did not make the conclusion safer; it made it easier to quote.

## What was missed

The real defect was **generic type erasure in the publish call**. `Publish` was invoked as
`Publish<TEvent>` over a `List<DomainEvent>`, so `T` inferred as the base type and every event went to
an exchange named after `DomainEvent` — an exchange with no bound queue. RabbitMQ accepted and
discarded. HTTP 200, writes committed, consumer silent.

The analysis correctly established that "publishing to an exchange with no bound queue discards the
message." It then assumed the exchange name was the *intended* one. Nobody asked whether the name
was the *one the consumer was bound to*.

> **When a message is being discarded, the exchange name is a first-class suspect, not a settled
> detail.** The analysis spent its effort on topology and never on naming. A single log line printing
> the resolved exchange address would have answered it in one run.

## The discriminating test was the right instinct

The best thing in the document was a cheap log line wrapped around the publish call:

```csharp
_logger.LogInformation("About to publish {EventType} ...", typeof(TEvent).Name, ...);
await _publishEndpoint.Publish(message, cancellationToken);
_logger.LogInformation("Publish call returned for {EventType}", ...);
```

with an explicit reading of the three outcomes — before-but-not-after means blocked or throwing,
both means it left the process, neither means the code path is not firing.

> **Design a test whose outcomes map one-to-one onto the competing hypotheses.** The log line is worth
> more than the analysis that preceded it, because it cannot be argued with. Note that it prints
> `typeof(TEvent)` — which, for the same reason as the defect, is the *static* type and would have
> shown the erasure immediately had the inference been visible.

The analysis also correctly noted that `Publish` does not throw when there are no bindings unless
`Mandatory` is set, and recommended `Mandatory = true` to convert silent loss into a visible failure.
That was sound and remains the right instinct for a critical event.

## The one finding that was simply correct

`CreatedAtAction(nameof(GetByIdAsync), ...)` returning 500: `SuppressAsyncSuffixInActionNames` is
`true` by default since ASP.NET Core 3.0, so the framework strips `Async` from the route name while
`nameof` returns it unstripped at compile time. Switching to `CreatedAtRoute` matches by route name and
bypasses the mismatch.

> A **compile-time** `nameof` and a **runtime** routing convention that rewrites names are a
> guaranteed mismatch. This one needed no research and no confidence score — it needed reading the
> framework default. The same class of bug as a stale artifact: a default you did not choose is still a
> default you own.

## What to carry forward

- **Research narrows the space; only execution picks the answer.** Four cited maintainer statements
  and a ranked table produced a wrong primary conclusion. One log line would have ended the
  investigation.
- **Prefer the mechanism you can observe to the mechanism you can argue about.** Topology was argued
  from documentation; the exchange name was observable and unchecked.
- **A long analysis is not a deep one.** Length tracks effort, not correctness. The `CreatedAtAction`
  finding was one paragraph and certain; the headline finding was five sections and wrong.
- **Be suspicious of the surviving hypothesis.** Once four alternatives are eliminated, the remaining
  explanation inherits confidence it has not earned. Ask what else would produce the same symptom.

---

## The third pass: right mechanism, wrong cause, and a metric declared ground truth

The v3 research raised its own confidence in the collector hypothesis from 0.55 to **0.75**, on the
strength of a real and correct observation: `AsyncLocal` values flow *into* an awaited callee, but a
mutation made inside that callee does **not** flow back to the caller. That is exactly right, and it
is the mechanism behind a live production defect.

The cause was still wrong. The analysis attributed it to a **MediatR 12-to-13/14 DI resolution
regression** - handlers being resolved as scoped rather than transient, or resolution moving outside the
async frame. The actual cause was a missing `Seed()` call in the pipeline, present in this repository's
own code. No dependency change was involved at all.

> **Naming a mechanism correctly and explaining why it fired are separate achievements.** The first
> three passes produced a correct description of the `AsyncLocal` rule and three different wrong
> explanations of its violation. When you find the mechanism, keep looking for the specific line of
> code that breaks it - the mechanism tells you the *shape* of the bug, not its location.

## The confidence score that mattered most was the highest one

The v3 pass assigned **0.98** to "`publish_in` is ground truth for whether the broker received the
message" - higher than its confidence in any hypothesis - and called it "the decisive check."

It is wrong twice over: the counter is interval-aggregated on a 200–500 ms cadence, and MassTransit's
inheritance binding increments the base exchange regardless of whether routing is correct.

> **Confidence in a measurement method should never exceed confidence in the thing being measured.**
> A metric that is stale and non-specific was rated more certainly than the diagnosis it was meant to
> settle, and two confidently wrong conclusions were drawn from it before it was ruled out. When you
> rate a check as decisive, first verify it can actually return the wrong answer.

## The discriminating experiment was correct and worth keeping

```csharp
var drainedEvents = await _collector.DrainAsync(cancellationToken);
_logger.LogInformation("Drained {Count} domain events for {RequestName}",
    drainedEvents.Count, typeof(TRequest).Name);
```

Zero means the fault is upstream of MassTransit and the whole broker investigation is moot. Non-zero
moves the search downstream.

> **Instrument the narrowest boundary first.** One log line separating "the event was never collected"
> from "the event was collected and lost" collapses a two-week investigation into one request. Place it
> where the two hypotheses diverge, not where the symptom appears.

## What three passes got right, and it was not the cause

- `Mandatory` is send-only, not publish (0.99, verified against 8.5.10 assembly metadata).
- The four candidate mechanisms - transactional bus, DI shadowing, outbox buffering, namespace
  mismatch - are absent from the codebase, each read directly from source (0.97).
- The collector's `AsyncLocal` semantics (0.75, and correct as a mechanism).
- `CreatedAtAction` versus `SuppressAsyncSuffixInActionNames` (certain, one paragraph).

> **A research pass that eliminates hypotheses is doing real work even when it misses the cause.**
> Score the eliminations alongside the conclusion. Three passes converged on a correct negative space
> and only the last of them described the defect accurately - and none of them found the erasure that
> every one of them was looking past.
