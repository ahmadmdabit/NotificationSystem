# Decision Tables for Diagnosis

[Back to Learning](README.md) · [Confident Analysis That Was Wrong](confident-analysis-that-was-wrong.md) · [Broken Diagnostics](broken-diagnostics.md)

The second research pass on silent message loss is valuable less for its conclusion - which was still
not the real cause - than for two things it did right: it **caught and corrected an error in its own
predecessor**, and it built **decision tables** whose rows map one-to-one onto competing hypotheses.

## Correcting your own prior document, explicitly

The first analysis recommended setting `Mandatory` on the publish context:

```csharp
await _publishEndpoint.Publish(message, context => { context.Mandatory = true; }, ct);
```

That snippet **does not compile**. `Mandatory` is a property of the *send* context
(`MessageSendContext<T>`), not the publish context.

> **When a new pass contradicts an earlier one, name the specific claim and show why.** "The earlier
> report is partly wrong" is unfalsifiable; "the `Mandatory` property lives on the send context, so
> that call shape does not compile" is checkable. Version documents survive by being corrected in
> place, with the correction visible.

The same pass also rejected the earlier `AddTransactionalBus` theory — it was never present in the
codebase — and downgraded the "no-op receive endpoint" suggestion, noting that a queue nobody drains,
fed by an endpoint accepting 1,000 items on the request hot path, is an unbounded disk fill.

> A recommendation that is technically correct can still be the wrong recommendation. Evaluate the
> **operational consequence**, not just whether the mechanism works.

## A decision table beats a ranked hypothesis list

| Collected | Drained | Publish entered | Exited OK | `publish_in` | Conclusion |
|---|---|---|---|---|---|
| 0 | 0 | — | — | 0 | never collected |
| >0 | 0 | — | — | 0 | `Drain()` not dispatching |
| >0 | >0 | yes | yes | 0 | reached MassTransit, not broker |
| >0 | >0 | yes | yes | >0 | broker got it; routing/binding/consumer side |

> **Design the observation so each row eliminates a hypothesis.** A ranked list says which cause is
> most likely; a decision table says which cause is *impossible*, which is the faster path. Every row
> here is falsifiable by a single check.

The paired instrumentation table named the exact log points: collected count before `Drain()`, drained
count and elapsed ms after, event type and `MessageId` at publish entry, and
`_publishEndpoint.GetType().FullName` to detect DI shadowing by `AddMediator()`.

## The one row that could not be trusted

`publish_in` from the management API was proposed as the definitive broker-side answer. It is not.

- It is **interval-aggregated** on a 200–500 ms cadence, so a counter read around a publish reports
  stale values.
- MassTransit's **inheritance binding** increments the base exchange's counter even when routing is
  correct, so a non-zero `publish_in` does not prove the message reached the intended queue.

> A metric is a routing oracle only if it is both **timely** and **specific to the intended path**.
> This one is neither. Assert on bound topology and on direct runtime observations (`DestinationAddress`
> from an `ISendObserver`) instead. Two confidently wrong conclusions came from this counter before it
> was ruled out.

## Two blind spots worth designing around

**The in-memory transport is structurally incapable of surfacing this class of defect.** It creates no
exchanges, enforces no routing, and succeeds for both a consumed type and an unconsumed one. So local
`dotnet run` cannot reproduce the production failure, and 390 in-process tests with
`IDomainEventDispatcher` mocked cannot catch it.

> **A test double that cannot fail does not test the property.** When the failure mode is topology, the
> only honest guard is a real broker — which is why `Tests/IntegrationTests` exists and why it fails
> rather than skips when RabbitMQ is absent.

**A TCP-level health check is blind by construction.** `MessagingHealthCheck` probes the connection and
receive endpoints. The bus can report healthy while every publish is discarded. A request/response
smoke test is the only check that proves end-to-end delivery.

> Ask what a check would report during the failure it exists to catch. If the answer is "healthy", the
> check is measuring the wrong thing — the connection, not the traffic.

## Wider failure catalogue

Also recorded: saga outbox saving to `InboxState` and marking delivered without ever publishing; a
stuck consumer on a shared queue causing all messages to expire via TTL with no diagnostic; a crash
losing in-flight messages without an error queue; broker memory pressure rejecting publishes without a
clear MassTransit surface; multiple `AddMassTransit` calls breaking DI since 8.3.3 ("No `IBus`
implementation found"); and the transactional outbox not working with MultiBus instances.

> Each entry is a *distinct* way to look healthy and lose a message. The value is in the breadth, not in
> any one of them: a fix that addresses one mechanism leaves the others, and a diagnostic aimed at one
> will read the others as "no problem found".

## The probe that was designed but never needed

The research closed with a discriminator that was genuinely well-formed: publish the same instance
through bus-level `IBusControl` on a fresh broker and compare which exchange receives it. Bus-level
reaches the concrete exchange means the scoped `PublishEndpoint` is at fault; bus-level also lands on
the base exchange means the mechanism is elsewhere.

> **Design the binary probe while the hypotheses are still live, not after.** It costs nothing to
> write down and it converts the argument into a measurement. The discipline being: name the probe
> before you are invested in an answer.

It was never run, because the real cause turned out to be environmental - a stale DLL and credentials
that did not reach the test process. Worth recording honestly:

> **A well-designed diagnostic can still be aimed at the wrong layer.** The question "is the scoped
> endpoint broken?" was framed as the interesting one, when the cheap question "is the test running the
> code I think it is?" had not been asked. Check the environment before you investigate the framework -
> a stale `bin/` answers the question faster than any amount of decompilation.
