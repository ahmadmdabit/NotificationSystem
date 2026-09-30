# Broker-Backed Integration Guards

[Back to Learning](README.md) · [Testing](../testing.md)

A real-broker guard exists because the defect it protects against is **invisible to every
in-process substitute**: a mocked publish endpoint accepts anything, and the in-memory transport
never reaches exchange-naming logic. Only a real broker distinguishes *"the consumer received it"*
from *"the message went somewhere unbound"*.

## Fail, never skip

When the broker is absent the suite **throws** with the recovery command. A guard that skips
unnoticed is indistinguishable from no guard — which is exactly how the generic-type-erasure defect
shipped behind a fully green suite. The guard is deliberately part of `NotificationSystem.slnx`, so
a repository-wide `dotnet test` requires a broker.

That is a real cost, accepted on purpose: a contributor without Docker sees a failure that is not a
defect. The alternative (skip with a warning) rebuilds the exact trap being guarded against.

## Credentials must come from the same file Compose reads

`docker compose` reads `.env` automatically. `dotnet test` does not. Left alone, the container runs
with the real account while the test process falls back to `guest`, which RabbitMQ refuses
off-loopback — producing `Broker unreachable: guest@...` wrapping an inner `ACCESS_REFUSED`, which
reads like a transport fault and is not one.

`TestBroker` therefore resolves in this order:

1. environment variable (lets CI override without a file)
2. **the repository `.env`**
3. `guest`

Two details that are load-bearing:

- **Locate `.env` by walking up from `AppContext.BaseDirectory`**, stopping at the solution file.
  Not the current directory — `dotnet test` launches each test host with an unpredictable working
  directory, so a relative path makes the fallback silently not apply. That failure looks exactly
  like the bug being fixed.
- **Parse minimally** — no interpolation, no shell expansion. A broken parse must fall through to a
  loud auth failure, never to a silently wrong credential.

## Observe the send side before the receive side

Every early failure said only *"the message did not arrive"*, which cannot distinguish
"published to an exchange nothing is bound to" from "published correctly and lost in transit" —
and both were blamed on the transport. An `ISendObserver` recording
`SendContext.DestinationAddress` plus the **static and runtime** message types names the exchange
directly and identifies erasure outright.

`DeliveryTrace.Localise()` checks the send side **first**. If nothing was ever handed to the
transport, a "nothing arrived" verdict is a conclusion about the wrong component.

> Note: `SendContext` exposes `DestinationAddress`. There is no `GetSendAddress()`.

## Prove the guard with a mutation

| Mutation | Expected |
|---|---|
| `Publish(domainEvent, ...)` — the `(object)` cast removed | **red** — `STAGE -1 (SEND) FAILED` |
| Cast restored, `git diff` clean, rebuild | green |

The mutation targets the **production dispatcher**, not a test double, so a green run proves the
dispatcher still routes on the runtime type. Re-run it after any change to the guard itself.

## Housekeeping that prevents false failures

- **Queue name is unique per process** (a `Guid` prefix), so a leaked or concurrent host cannot
  consume this run's messages. A leaked host broke every later run before this was added.
- **Do not set `AutoDelete`** on the receive endpoint: an auto-delete queue is removed whenever the
  last consumer disconnects, including the brief window during bus startup, so a message published
  into that window reaches an exchange with no binding and is discarded. This caused intermittent
  failures until it was removed.
- **Wait for a consumer to be attached before publishing.** The queue declaration and its binding
  are broker round trips; publishing immediately can beat the binding into existence — the very
  shape the guard exists to detect.
- **Match on payload, not just arrival**, so a leftover message from a previous run cannot satisfy
  a wait. That removes the need to purge and the privileged management call that goes with it.
- **No counters.** See [publish_in is not a routing signal](masstransit-publish-routing.md).

## Running it

```bash
docker compose -f docker-compose.test.yml up -d --wait rabbitmq
dotnet test Tests/IntegrationTests/IntegrationTests.csproj -c Debug
```

The test broker is host-published on `5673`/`15673` because the tests run on the host; the main
stack owns `5672`. `docker compose up` requires the variables in `.env` - on Windows Docker may not
be on `PATH` and runs inside WSL2 (`wsl -d ubuntu -- docker ...`).

## The two-hop topology is not optional

MassTransit does **not** bind a publish exchange directly to a queue. Verified with
`rabbitmqctl list_bindings source_name destination_name destination_kind`:

```
TestDoubles.Stubs:TestDomainEvent      ->  integration-test-domain-event-25aa3117   (exchange)
integration-test-domain-event-25aa3117  ->  integration-test-domain-event-25aa3117  (queue)
```

A diagnostic asking "which **queues** are bound to the publish exchange" answers `NONE` on a
perfectly healthy bus, because the first hop targets an *endpoint exchange*. One here printed `NONE`
next to `consumers=1` - a self-contradictory line in the same output.

> **Any topology assertion must traverse both hops.** This is also why a unique queue name cannot
> prevent cross-run interference: contention lives on the shared *publish* exchange, not the queue.

Because the publish exchange is **fanout**, a stale binding from an orphaned run silently takes a
copy of every message. See [Broken Diagnostics](broken-diagnostics.md).

## Two distinct defects with an identical symptom

The guard failed 0/2: endpoint ready, consumer attached, publish accepted, `IReceiveObserver.PreReceive`
never invoked, consumer body never ran. Two different causes produced exactly that signature.

1. **Stale build output** - `bin/`+`obj/` sat beside the source holding an outdated
   `Shared.Infrastructure.dll`, which shadowed the code under test.
2. **Credentials not propagated** - covered above; the container read `.env`, the test process did not.

A third was found and fixed along the way, and is a genuine bug independent of both:

## The scope must be created after the bus starts

`IPublishEndpoint` is registered **scoped**, and the scoped instance is the lazily constructed
`BusScopedBusContext.PublishEndpoint`, which **captures its provider on first access**. The harness
created the scope *before* `bus.StartAsync()` and resolved the dispatcher inside it, binding the
dispatcher to a bus that was not yet running.

```csharp
await bus.StartAsync();
await TestBroker.WaitForConsumerAttachedAsync(TestBroker.QueueName, TopologyTimeout);
var scope = provider.CreateAsyncScope();   // AFTER start - load-bearing
```

> **In a hand-built harness, construction order is part of the contract and nothing enforces it.** A
> lazily constructed scoped service captures whatever was true at first access. This is the same
> trap as the production `AddExceptionHandler` defect, in miniature: the wiring compiles, the container
> resolves it, and the wrong instance is captured silently.

> **One symptom can have several causes, and the guard cannot tell you which.** Publish accepted, never
> delivered was produced by a stale DLL, an auth rejection, and a scope-ordering bug. Because the
> symptom is shared, the investigation has to establish the *environment* before it diagnoses the
> *code* - and a hand-built `ServiceProvider` is exactly where scope-ordering bugs live.

## Instrument the send side, or you will blame the broker

The original localisation checked only receive stages. With the cast removed, the mutation failed at
**Stage -1 (send)** - nothing ever reached the transport. The old localisation would have reported a
broker-side delivery failure and sent the investigation to the transport, which was not at fault.

```csharp
bus.ConnectSendObserver(new SendAddressObserver());   // records DestinationAddress
```

`Localise()` now checks the send side before any receive stage and reports type erasure explicitly.

> **Localise at the earliest stage that can distinguish the hypotheses.** A diagnostic that only
> observes the far end of a pipeline attributes every upstream failure to the far end. One
> `ISendObserver` on `DestinationAddress` is the difference between "the broker dropped it" and
> "we never sent it anywhere real".
