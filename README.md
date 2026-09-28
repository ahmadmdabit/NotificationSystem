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

Start with [Architecture](documents/architecture.md) for the component diagram, and
[Getting Started](documents/getting-started.md) to run it.

> ⚠️ **Two things to know before you run anything.** MassTransit is commercially
> licensed and the stack will not start without a key — see
> [Messaging & Dispatch](documents/messaging.md#licence-requirement). And a repeat
> `Notifications/Send` is now idempotent rather than a 500 — see
> [Stored Procedures](documents/database.md#stored-procedures).

## License

Licensed under the [MIT license](LICENSE.md).
