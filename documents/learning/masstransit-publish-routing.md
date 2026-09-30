# MassTransit Publish Routing

[Back to Learning](README.md) · [Messaging & Dispatch](../messaging.md)

## The defect this prevents

`TransactionBehavior` drains a `List<DomainEvent>` and publishes each element. The list's element
type is the **abstract** `DomainEvent`, so the compiler infers `T = DomainEvent` for `Publish<T>`.
MassTransit derives the publish exchange from that **static** generic argument, not from
`message.GetType()`, so every event went to `Shared.Domain:DomainEvent` — an exchange with no bound
queue.

The failure was invisible: RabbitMQ accepted the message and discarded it. No exception, HTTP 200,
the write committed, the queue stayed empty, and the consumer simply never ran. A 390-test suite
stayed fully green throughout.

## The fix, and why it is not a workaround

```csharp
await publishEndpoint.Publish((object)domainEvent, cancellationToken).ConfigureAwait(false);
```

The cast selects the non-generic `IPublishEndpoint.Publish(object)` overload, which calls
`message.GetType()` and re-enters the generic publish with the **concrete** type. Verified against
the shipped 8.5.10 assemblies:

```
MassTransitBus.Publish(object)            -> new PublishEndpoint((IPublishEndpointProvider)_receiveEndpoint)
PublishEndpoint.Publish(object)          -> Type type = message.GetType()
PublishEndpointConverterCache.Publish    -> Converters.Value[messageType]
PublishEndpointConverter<T>.Publish      -> if (message is T m) endpoint.Publish(m)   // re-enters GENERIC
```

**Never remove that cast.** It is not a style choice; it is the only thing that makes the exchange
name correct. A "simplification" back to `Publish(domainEvent)` restores the silent discard, and
**no unit test can catch it** — a mocked `IPublishEndpoint` accepts anything, and the in-memory
transport never reaches naming logic at all.

## Two properties that are easy to get wrong

- **`IPublishEndpoint` is scoped.** The scoped instance is `BusScopedBusContext.PublishEndpoint`,
  built lazily from the per-scope provider. Resolving it before `bus.StartAsync()` hands the caller
  an endpoint bound to a bus that is not yet running — publish is accepted and delivered nowhere.
  Production never hits this (the dispatcher is resolved per request); a hand-built test harness does.
- **The scoped endpoint shares one cache with the bus.** It delegates to the bus's own provider, so
  both routes hit the same `SendEndpointCache<Type>` instance. There is no second cache and no
  per-scope topology snapshot, so a scope cannot "pin" a base-type entry. This refuted a plausible
  root-cause theory that documentation had promoted.

## `publish_in` is not a routing signal

RabbitMQ's `message_stats.publish_in` is **interval-aggregated** (200–500 ms) *and* MassTransit's
**inheritance binding** declares a concrete→base binding, so the base exchange accumulates
`publish_in` even when routing is completely correct. A counter that reads 2 on the base exchange is
consistent with correct routing and with erasure. It cannot tell you which.

Assert on **bound-queue topology** (does this exchange reach this queue through the real binding
chain?) — never on counters.

## Writing a test that can actually fail

The publish must go through a **`DomainEvent`-typed variable**:

```csharp
DomainEvent domainEvent = TestDomainEventFactory.Create("guard");   // erasure only happens this way
```

Written as `PublishAsync(new TestDomainEvent(...))` the compiler infers the concrete type, the
exchange is right, and the test would **pass against the broken dispatcher**.

## The erasure, restated as a search problem

The first analysis spent five sections on topology and never asked one question: **what is the exchange
name?** The erasure is a naming defect, and every diagnostic that printed a name would have exposed it
immediately.

| Layer | What it sees |
|---|---|
| Handler | the concrete event |
| Collector drain | `List<DomainEvent>` - base type |
| Generic inference | `TEvent` = `DomainEvent` |
| Broker | `Shared.Domain:DomainEvent` - no bound queue |

> **When a message vanishes at a broker, log the resolved address, not the type you think you sent.**
> `DestinationAddress` from an `ISendObserver` is the ground truth. A log line printing
> `typeof(TEvent).Name` in the dispatcher would also have shown it - the erasure is visible in the
> static type long before it becomes a routing failure.

## The two fixes that were both right for different reasons

- **The cast** `Publish((object)domainEvent, ...)` - forces the runtime type. This is the actual fix.
- **`Mandatory = true`** - makes an unroutable message throw instead of vanishing.

They address different failures and are not substitutes. The cast fixes the misrouting; `Mandatory`
would only have converted a *correct* publish to a deliberately unrouteable event into a loud error.
Adopting `Mandatory` alone would have produced a working system that throws on every publish.

> Add `Mandatory` to make future silent loss loud, but only after routing is correct - otherwise you
> have built an outage with good error messages.

## The topology fact that was right and still was not the cause

`ConfigureEndpoints` creates queues only for registered consumers; a pure publisher never gets a bound
queue, and its messages are discarded. True, in both v8 and v9, and it is why
`NotificationSentEvent` has no queue. But that is the **designed** behaviour of a service with no
consumer, not a defect - and it was never the cause of the loss being investigated, which involved an
event that *did* have a consumer.

> **A true statement about the system is not thereby an explanation of the symptom.** Verify that the
> fact you found is on the path that actually failed before building a fix on it.
