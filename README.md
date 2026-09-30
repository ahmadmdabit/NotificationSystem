# NotificationSystem

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/ahmadmdabit/NotificationSystem)
[![.NET](https://img.shields.io/badge/.NET-10-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE.md)

A notification system built with .NET 10 as microservices: `UserService` and
`NotificationService` behind an Ocelot gateway, with an MVC UI acting as a BFF. Clean
Architecture, CQRS and DDD, SQL Server via Dapper, enforced by ArchUnitNET.

## Quick start

```bash
git clone https://github.com/ahmadmdabit/NotificationSystem.git
cd NotificationSystem
cp .env.example .env      # then set the five required secrets
docker compose up -d --build
docker compose ps         # all services should report (healthy)
```

Running without Docker, or building only, is covered in
[Getting Started](documents/getting-started.md).

## Documentation

| Document                                        | What it covers                                                                                                           |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| [Architecture](documents/architecture.md)       | Overview, feature list, component diagram, tech stack, project layout, domain entities, layering, cross-cutting concerns |
| [Getting Started](documents/getting-started.md) | Prerequisites, build, JWT secret generation, running locally or with Compose, service ports                              |
| [API Reference](documents/api-reference.md)     | Swagger endpoints, endpoint surface and authorization, error contract and diagnostic redaction, security model           |
| [Database](documents/database.md)               | Engine and schema, reset procedures, stored procedures and their contract                                                |
| [Messaging & Dispatch](documents/messaging.md)  | MassTransit licensing, the messaging on/off switch, transports, post-commit domain event dispatch                        |
| [Development](documents/testing.md)             | Architecture details for contributors, test projects, regression guards, TUnit pitfalls, adding a feature                |
| [Conventions](documents/conventions.md)         | Engineering rules and why each exists: layering, persistence, error contract, testing, pitfalls                          |
| [Operations](documents/operations.md)           | Frontend dependencies, CI/CD workflow, compose test environment                                                          |
| [Learning Notes](documents/learning/README.md)   | Twenty topic notes: honest verification, publish routing, broker guards, idempotency, flaky tests                       |

Start with [Architecture](documents/architecture.md) for the component diagram, and
[Getting Started](documents/getting-started.md) to run it.

> ⚠️ **Two things to know before you run anything.** `Notifications/Send` is idempotent
> rather than a 500 — see [Stored Procedures](documents/database.md#stored-procedures). And
> domain events are published on the *runtime* type, so a `DomainEvent`-typed variable routes
> to the concrete exchange rather than silently vanishing — see
> [Messaging & Dispatch](documents/messaging.md#publish-on-the-runtime-type).

## Tests

```bash
docker compose -f docker-compose.test.yml up -d --wait rabbitmq   # the guard needs a real broker
dotnet test NotificationSystem.slnx -c Debug                     # 392 tests, including the guard
```

`Tests/IntegrationTests` publishes domain events through the production dispatcher to a real
RabbitMQ broker, which is the only way to catch a routing regression: a mocked publish endpoint and
the in-memory transport both fail to observe exchange naming. It fails rather than skips when the
broker is absent, and it reads broker credentials from the environment or the repository `.env` -- the
same file `docker compose` used, so the two cannot disagree. See
[Development](documents/testing.md#broker-backed-coverage).

> The run still reports `error: 1` and exits non-zero even when every test passes:
> `Tests/UnitTests/TestDoubles` is a class library with no tests, and `global.json`'s `test` node
> supports only `runner`, so it cannot be excluded. **Gate on `failed: 0`.**

## License

Licensed under the [MIT license](LICENSE.md).
