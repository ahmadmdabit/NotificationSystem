# Messaging & Dispatch

[Back to README](../README.md)

> ⚠️ **MassTransit 9.2.2 is commercially licensed and will not start without a key.** This is
> pre-existing, not introduced by any recent change, and it is the single most common reason
> `docker compose up` cannot reach `healthy`. Read this section before debugging anything else.

### Licence requirement

Both `.Infrastructure.csproj` files pin `MassTransit` / `MassTransit.RabbitMQ` **9.2.2**. The licence
gate runs **when the bus is created, before the transport is chosen**, so it applies to the in-memory
transport too:

```
Unhandled exception. MassTransit.ConfigurationException: The bus configuration is invalid:
   [Failure] License must be specified with SetLicense/SetLicenseLocation or by
   setting the MT_LICENSE/MT_LICENSE_PATH environment variables.
```

There is **no development, CI, test or evaluation exemption** — not one environment is exempt.
Switching to `Messaging__UseRabbitMq=false` does **not** avoid it.

**Supported mechanism (recommended) — a mounted key file.** Nothing secret enters the repo:

```yaml
# docker-compose.yml — already present for both services
environment:
  - MT_LICENSE_PATH=/masstransit/license.txt
volumes:
  - type: bind
    source: ${MASSTRANSIT_LICENSE_FILE:-${HOME}/.dotnet/MassTransit/license.txt}
    target: /masstransit/license.txt
    read_only: true
    bind:
      create_host_path: false
```

Put the key at `~/.dotnet/MassTransit/license.txt`, or point `MASSTRANSIT_LICENSE_FILE` elsewhere.
Validation is local and offline — no activation server. **Never** put the key in a `Dockerfile`,
`ARG`, `ENV`, `appsettings`, or a committed file.

> **Create that file before your first `docker compose up`, even with messaging off.** The bind mount
> is unconditional, and `create_host_path: false` turns a missing source path into a clear error
> instead of letting Docker silently create a _directory_ there — which it did, and which then broke
> the mount with an opaque failure rather than the documented "License must be specified":
>
> ```bash
> mkdir -p ~/.dotnet/MassTransit && touch ~/.dotnet/MassTransit/license.txt
> ```
>
> A missing key and a _malformed_ key fail differently, and the difference is how you confirm the
> path is being read: `License must be specified` means the path was **not** resolved;
> `The license could not be loaded: The input is not a valid Base-64 string…` means the path **was**
> read and the contents are invalid. Verified against a running container 2026-09-28.

### Messaging on/off

`Messaging:Enabled` (`Messaging__Enabled` as an env var, `MESSAGING_ENABLED` in compose) switches the
whole bus registration:

> ⚠️ **The two names are not interchangeable, and `.env` needs the second one.**
> `docker-compose.yml` sets `Messaging__Enabled=${MESSAGING_ENABLED:-true}` as an explicit
> container environment entry, so it **overwrites** whatever `.env` holds under that name.
> Use `MESSAGING_ENABLED=false` in `.env` for compose; use `Messaging__Enabled=false` only when you
> `dotnet run` an API directly and have it exported. Setting only `Messaging__Enabled` in
> `.env` leaves messaging **on** in the containers, and you get the licence crash-loop below.

| Value                        | Behaviour                                                                                                                                                                                               |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `true` (default)             | Bus is registered. **Requires a licence key.**                                                                                                                                                          |
| absent, `""`, or unparseable | Treated as `true` — an absent key never silently disables the broker.                                                                                                                                   |
| `false`                      | **No MassTransit registration at all.** `NullDomainEventDispatcher` is registered instead. Services start, `/health` answers, domain events are **dropped** and logged at Debug. No broker, no licence. |

```bash
# start the stack without a licence (local dev / CI)
MESSAGING_ENABLED=false docker compose up -d
```

`NullDomainEventDispatcher` is not optional decoration. Without a bus there is no `IPublishEndpoint`,
so `MassTransitDomainEventDispatcher` would be unresolvable and the **post-commit dispatch path in
`TransactionBehavior` would fail** — losing events is a far better failure than a dead pipeline.
`MessagingHealthCheck` reports messaging as disabled rather than probing a broker that is not there,
so a stopped bus is visible in `/health` instead of silently healthy.

> **Not a deployment mode.** With messaging off, events are discarded, not queued. Keep it enabled in
> production.

### Transport

Selected by `Messaging:UseRabbitMq` (`Messaging__UseRabbitMq` as env var / `.env` key):

- **Local dev** (default): in-memory transport — no broker required (but a licence _is_, unless
  `Messaging:Enabled=false`).
- **Docker/production**: RabbitMQ transport, injected by `docker-compose.yml` via `Messaging__UseRabbitMq=true` + `Messaging__RabbitMq__Host=rabbitmq` + `Messaging__RabbitMq__Username`/`Password` for both services.

**Broker credentials** (`.env.example` → `.env`):

- `RABBITMQ_USER` / `RABBITMQ_PASSWORD` — compose maps these to RabbitMQ's `RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS`. RabbitMQ's default `guest` account is loopback-only and **refused** for remote (bridge-network) connections, so a non-guest pair is required when `Messaging__UseRabbitMq=true`.
- RabbitMQ ports: `5672` (AMQP). Management UI is available on `15672` (not forwarded in compose).

**Post-commit dispatch** (no ghost events on rollback):

1. Aggregate domain events (`UserRegisteredEvent`, `NotificationSentEvent`) carry their payload and are raised during command-handler execution.
2. Handlers record events on `Shared.Domain.DomainEventCollector` — an `AsyncLocal<List<DomainEvent>>` that isolates concurrent async flows per-request.
3. `Shared.Application.Behaviors.TransactionBehavior<TRequest, TResponse>` wraps every `ICommand` in an `IUnitOfWork` transaction. **Before** invoking the handler it calls `DomainEventCollector.Seed()`. Only after `_unitOfWork.CommitAsync()` succeeds does it call `DomainEventCollector.Drain()` and publish each event via `MassTransitDomainEventDispatcher` (which wraps MassTransit's `IPublishEndpoint`).
4. On rollback, `DomainEventCollector.Clear()` discards pending events — they are never published.

> ⚠️ **`Seed()` is load-bearing.** `AsyncLocal` values flow _into_ an awaited callee, but a mutation made inside it does **not** flow back to the caller. Without seeding, the handler's `Add()` allocates a list the pipeline cannot see, the post-commit `Drain()` returns empty, and **every domain event is silently dropped** — writes commit, nothing is published. This was a live defect, fixed 2026-09-26. `TransactionBehaviorTests.Handle_HandlerAddsEventInsideNext_PublishesItPostCommit` guards it.

**Topology**:

- **`UserService`** **publishes** `UserRegisteredEvent` (after commit) and **consumes** it via `UserRegisteredEventConsumer` (registered with `cfg.AddConsumer<UserRegisteredEventConsumer>()` + `cfg.ConfigureEndpoints(context)`). The consumer's `Handle` method and `LogMessageTemplate` are `internal` — visible to `UserService.Tests` via `[assembly: InternalsVisibleTo("UserService.Tests")]` in `UserService.Infrastructure/Properties/AssemblyInfo.cs`, not on the public API surface. The attribute form is required because that project sets `GenerateAssemblyInfo=false`, which disables the `<InternalsVisibleTo Include="…"/>` item form used in `UI.csproj`.
- `NotificationService` **publishes** `NotificationSentEvent` (after commit). It has no consumer for this event — it is a pure publisher (notification history is written to SQL via `SPNotificationHistoryInsert` TVP, not consumed from the broker).

**Health**: `MessagingHealthCheck` (`Shared.Api`) has three outcomes:

- `Messaging:Enabled=false` → `Healthy` with "messaging is disabled; domain events are dropped".
- `Messaging:UseRabbitMq=false` → `Healthy` with "in-memory transport in use; no broker required".
- RabbitMQ active → TCP connect probe to `Messaging:RabbitMq:Host`:`Port` (default `localhost:5672`).

Registered as the `"messaging"` health check in both services' `Program.cs`.

**End-to-end verification status** (2026-09-28, against a live container):

| Scenario                                      | Result                                                                                                                                                            |
| --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MESSAGING_ENABLED=false`, both services up   | **Pass** — both `healthy` in ~10 s, `/health` → `Healthy`, zero `MassTransit.ConfigurationException`                                                              |
| `MESSAGING_ENABLED=true` + malformed key file | **Pass** — fails with the Base-64 parse error, `License must be specified` count 0, proving `MT_LICENSE_PATH` is read                                             |
| `MESSAGING_ENABLED=true` + **valid** key      | **Not verified** — no licence key is available in this environment. Drop a key at `~/.dotnet/MassTransit/license.txt` and run `docker compose up -d` to close it. |
