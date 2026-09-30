# Investigating with Decompiled Source

[Back to Learning](README.md) · [Messaging & Dispatch](../messaging.md)

When a third-party library behaves unexpectedly, reading the shipped assemblies beats inference.
MassTransit 8.5.10 ships only `.dll` + `.xml` (no PDBs or source), but `ilspycmd` recovers
readable C# — and that settled in minutes what 62 web sources could not.

## Decompile everything, then grep

**Five targeted type lookups failed.** Assemblies, namespaces, and type names were all guessed
wrong; one "hit" turned out to be a saga-only `PublishExtensions` decoy, and `-l i` lists
interfaces only. The one pass that worked was decompiling **all three assemblies** and grepping the
result:

```powershell
dotnet nuget locals global-packages --list
# -> E:\packages-managers\NuGetCache\packages
ilspycmd -p -o <out> MassTransit.dll MassTransit.Abstractions.dll MassTransit.RabbitMqTransport.dll
ugrep -rn -e 'class PublishEndpoint' <out>
```

2984 files, ~30 s, and the answer appeared immediately.

**Why the guesses missed, and the lesson:**

> `MassTransit.Transports.PublishEndpoint` lives in **`MassTransit.Abstractions.dll`** — not
> `MassTransit.dll`, and not the RabbitMQ assembly. One assembly's fact explained every failed
> lookup, including a long dead end in `BaseReceiveEndpointContext`, which contained no `Publish` at
> all because it never had.

**Do not guess type names across assemblies. Dump everything and grep.**

## A thorough report can still be wrong at its centre

A 491-line research document concluded that scoped and bus-level `IPublishEndpoint` were "not
equivalent", with a per-scope cache able to pin a base-type entry. The source refuted it in three
lines — the scoped provider's inner provider **is the bus**, so both share one
`SendEndpointCache<Type>`:

```csharp
// BusScopedBusContext.cs:34
new PublishEndpoint(new ScopedPublishEndpointProvider((IPublishEndpointProvider)_bus, _provider))
```

**Volume of sources is not evidence.** A wrong central claim survives any number of citations.
Verify the load-bearing claim against the artifact before building on the rest.

## Separate confirmed from inferred

Maintain the distinction explicitly, because it is what got conflated:

- **Confirmed** — quoted from decompiled source with file and line.
- **Inferred** — plausible mechanism, no source.

Two items in the research were flagged "unresolved" and resolved here from source:
`ScopedSendPipeAdapter<T>.Send(SendContext<TMessage>)` has an **empty body** while the generic
`Send<T>` overload adds the payload (a genuine asymmetry, unrelated to routing), and
`ExchangeBindingConsumeTopologySpecification.Apply` confirms the two-hop binding
publish exchange → endpoint exchange → queue.

## What decompiling does not give you

Behaviour still has to be observed. Decompilation gave the *shape* of the publish path; the
`PreSend` observer gave the *actual exchange*. Both were needed, and neither substitutes for the
other.

## A hypothesis that decompiled source killed

The research proposed that `SendEndpointProvider`'s `SendEndpointCache<Type>` is keyed on the
**static** `typeof(T)`, so a base-type entry created first would pin routing to the base exchange even
after the `(object)` cast. The source line quoted in support is real:

```csharp
_cache.GetSendEndpoint(typeof(T), type => CreateSendEndpoint<T>())
```

It is simply not the mechanism, because `BusScopedBusContext` builds
`new ScopedPublishEndpointProvider((IPublishEndpointProvider)_bus, ...)` and that provider delegates
straight back to `_bus.GetPublishSendEndpoint<T>()`. **Both endpoints share one
`SendEndpointCache<Type>` instance.** There is no second cache and no separate topology snapshot, so a
scope cannot pin a base-type entry.

> **A plausible mechanism with a real code citation is still a hypothesis.** The quote proved the cache
> is keyed on `typeof(T)`; it did not prove a second cache existed, which is what the hypothesis needed.
> Ask what the claim requires that the evidence does not contain.

This is the strongest argument in the whole research record for reading source over inference: sixty-two
sources, a ranked mechanism, and a sequence diagram all pointed at a cache that does not exist twice.

## Reporting an unresolved contradiction is the honest move

The report stated plainly that two things were simultaneously true and could not be reconciled from
public documentation: the decompiled chain verifiably uses `message.GetType()`, and the broker showed
both publishes on the base exchange. It named the contradiction, gave a binary probe to settle it, and
refused to close the issue.

> **"Two verified observations disagree" is a legitimate finding.** The failure mode is not the
> contradiction - it is quietly dropping one observation because it is inconvenient, and then building
> on the survivor. Naming the tension, and naming the single experiment that resolves it, is more
> useful than a confident resolution that happens to be wrong.

The proposed probe was the right shape: publish the same instance through bus-level `IBusControl` on a
fresh broker and compare which exchange receives it. One run, two outcomes, no ambiguity.

> When a contradiction is unresolvable by reading, **design the experiment before proposing the fix.**
> A binary outcome converts an argument into a measurement - and here it would have cost one run
> instead of three documents.

## The real answer, for the record

The guard's failure was environmental, not structural: stale `bin/`+`obj/` holding an outdated
`Shared.Infrastructure.dll`, and credentials that never reached the test process. Neither the scoped
endpoint nor the send-endpoint cache was at fault, and the discriminator that would have settled the
question was never needed.
