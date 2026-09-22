# NotificationSystem

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/ahmadmdabit/NotificationSystem)
[![.NET](https://img.shields.io/badge/.NET-10-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE.md)

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Architecture Diagram](#architecture-diagram)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation](#installation)
  - [Running the Application](#running-the-application)
  - [Service Ports](#service-ports)
- [Project Structure](#project-structure)
- [Domain Entities](#domain-entities)
- [API Documentation](#api-documentation)
- [API Surface](#api-surface)
- [Security](#security)
- [Database](#database)
- [Error Contract](#error-contract)
- [Cross-cutting Concerns](#cross-cutting-concerns)
- [Frontend Dependencies](#frontend-dependencies)
- [CI/CD](#cicd)
- [Development](#development)
  - [Architecture Details](#architecture-details)
  - [Adding New Features](#adding-new-features)
- [License](#license)

## Overview

A modern, scalable notification system built with .NET 10 using a microservices architecture. This project demonstrates best practices in software design, including Clean Architecture, CQRS, DDD, and API gateway pattern implementation.

The application consists of multiple components:

- **Microservices**: UserService and NotificationService for handling business logic
- **API Gateway**: Centralized routing using Ocelot
- **Web UI**: MVC application with responsive design
- **Shared Kernel**: Common, Shared.Domain, Shared.Application, Shared.Infrastructure
- **Architecture Tests**: ArchUnitNET-based dependency rule enforcement

## Key Features

- **Notification Management**: Create, send, and track notifications
- **User Management**: User registration and profile management
- **Microservices Architecture**: Independent, scalable services
- **API Gateway**: Centralized request routing and management
- **API Documentation**: Interactive Swagger UI for all services
- **Responsive UI**: Modern web interface using Bootstrap and jQuery
- **Database**: SQL Server with Dapper ORM — LocalDB for local dev, SQL Server 2022 container in Docker
- **CORS Support**: Cross-origin resource sharing enabled
- **JWT Authentication**: HMAC-SHA256 token-based auth with 7-day expiry, validated by `JwtBearer` in both services. See [Security](#security).
- **Authorization**: `[Authorize]` on `BaseApiController` — all endpoints are authenticated by default except `Register`, `Authenticate`, and `/health` (`[AllowAnonymous]`)
- **Docker Containerization**: Multi-stage alpine images, non-root execution, health checks, and app-level schema migration via `IHostedService`

## Architecture Diagram

```mermaid
flowchart TD

subgraph group_clients["Client Experience"]
  node_ui_web["MVC Web UI<br/>[HomeController.cs]"]
  node_ui_api["UI API Facade<br/>[ApiController.cs]"]
  node_gateway_client["Gateway Client"]
end

subgraph group_edge["Gateway Edge"]
  node_gateway["API Gateway<br/>[Program.cs]"]
end

subgraph group_user_api["UserService.Api"]
  node_user_api["UsersController"]
end

subgraph group_user_app["UserService.Application"]
  node_user_cmd["RegisterUserCommand<br/>AuthenticateUserCommand"]
  node_user_query["GetUserByIdQuery<br/>GetAllUsersQuery"]
end

subgraph group_user_infra["UserService.Infrastructure"]
  node_user_repo["UserRepository<br/>Dapper + SP"]
  node_user_uow["UnitOfWork"]
  node_user_jwt["JwtTokenService"]
  node_user_pwd["PasswordHasher"]
end

subgraph group_user_domain["UserService.Domain"]
  node_user_entity["User Entity<br/>Password VO"]
  node_user_event["UserRegisteredEvent"]
end

subgraph group_notif_api["NotificationService.Api"]
  node_notif_api["NotificationsController"]
  node_history_api["NotificationsHistoryController"]
end

subgraph group_notif_app["NotificationService.Application"]
  node_notif_cmd["SendNotificationsCommand"]
  node_notif_query["GetNotificationByIdQuery<br/>GetNotificationHistoryQuery"]
end

subgraph group_notif_infra["NotificationService.Infrastructure"]
  node_notif_repo["NotificationRepository<br/>Dapper + SP"]
  node_history_repo["NotificationHistoryRepository<br/>TVP + SP"]
  node_notif_uow["UnitOfWork"]
  node_notif_publisher["NotificationSentEventPublisher<br/>MassTransit"]
end

subgraph group_notif_domain["NotificationService.Domain"]
  node_notif_entity["Notification Entity<br/>NotificationStatus VO"]
  node_notif_event["NotificationSentEvent"]
end

subgraph group_shared["Shared Kernel"]
  node_shared_domain["Shared.Domain<br/>DomainEvent, IRequest"]
  node_shared_app["Shared.Application<br/>IRequest, IDomainEventDispatcher"]
  node_shared_infra["Shared.Infrastructure<br/>InMemory/MassTransit Dispatcher"]
  node_common["Common<br/>ApiResult, ErrorResult, AppSettings"]
end

node_user_actor(("User"))
node_sql_server[("SQL Server<br/>LocalDB / 2022 container")]
node_rabbitmq[("RabbitMQ<br/>optional")]

node_user_actor -->|"uses UI"| node_ui_web
node_ui_web -->|"submits requests"| node_ui_api
node_ui_api -->|"calls client"| node_gateway_client
node_gateway_client -->|"sends HTTP"| node_gateway
node_gateway -->|"routes /Users"| node_user_api
node_gateway -->|"routes /Notifications"| node_notif_api
node_gateway -->|"routes /NotificationHistories"| node_history_api
node_user_api -->|"mediates"| node_user_cmd
node_user_api -->|"mediates"| node_user_query
node_user_cmd -->|"persists"| node_user_repo
node_user_query -->|"reads"| node_user_repo
node_user_repo -->|"uses"| node_user_uow
node_user_jwt -->|"generates"| node_user_entity
node_user_pwd -->|"hashes"| node_user_entity
node_user_event -->|"dispatches"| node_shared_infra
node_notif_api -->|"mediates"| node_notif_cmd
node_notif_api -->|"mediates"| node_notif_query
node_history_api -->|"mediates"| node_notif_query
node_notif_cmd -->|"persists"| node_notif_repo
node_notif_cmd -->|"streams TVP"| node_history_repo
node_notif_query -->|"reads"| node_notif_repo
node_notif_repo -->|"uses"| node_notif_uow
node_history_repo -->|"uses"| node_notif_uow
node_notif_event -->|"publishes"| node_notif_publisher
node_notif_publisher -->|"RabbitMQ"| node_rabbitmq
node_user_repo -->|"queries"| node_sql_server
node_notif_repo -->|"queries"| node_sql_server
node_history_repo -->|"inserts"| node_sql_server

classDef toneNeutral fill:#f8fafc,stroke:#334155,stroke-width:1.5px,color:#0f172a
classDef toneBlue fill:#dbeafe,stroke:#2563eb,stroke-width:1.5px,color:#172554
classDef toneAmber fill:#fef3c7,stroke:#d97706,stroke-width:1.5px,color:#78350f
classDef toneMint fill:#dcfce7,stroke:#16a34a,stroke-width:1.5px,color:#14532d
classDef toneRose fill:#ffe4e6,stroke:#e11d48,stroke-width:1.5px,color:#881337
classDef toneIndigo fill:#e0e7ff,stroke:#4f46e5,stroke-width:1.5px,color:#312e81
classDef toneTeal fill:#ccfbf1,stroke:#0f766e,stroke-width:1.5px,color:#134e4a
class node_ui_web,node_ui_api,node_gateway_client,node_user_actor toneBlue
class node_gateway,node_sql_server,node_rabbitmq toneAmber
class node_user_api,node_user_cmd,node_user_query toneMint
class node_user_infra,node_user_repo,node_user_uow,node_user_jwt,node_user_pwd toneTeal
class node_user_domain,node_user_entity,node_user_event toneIndigo
class node_notif_api,node_history_api,node_notif_cmd,node_notif_query toneRose
class node_notif_infra,node_notif_repo,node_history_repo,node_notif_uow,node_notif_publisher toneTeal
class node_notif_domain,node_notif_entity,node_notif_event toneIndigo
class node_shared_domain,node_shared_app,node_shared_infra,node_common toneNeutral
```

## Tech Stack

| Layer                 | Technology                                           |
| --------------------- | ---------------------------------------------------- |
| **Framework**         | ASP.NET Core 10 (net10.0)                            |
| **Architecture**      | Clean Architecture + CQRS + DDD, Microservices       |
| **Languages**         | C#                                                   |
| **Database**          | SQL Server 2022 (Docker) / LocalDB + Dapper         |
| **API Gateway**       | Ocelot 25.0.1                                        |
| **Container**         | Docker (multi-stage alpine, non-root, health checks) |
| **Frontend**          | ASP.NET Core MVC (Bootstrap, jQuery, Grid.js)        |
| **API Documentation** | Swagger/OpenAPI                                      |
| **HTTP Client**       | RestSharp                                            |
| **Messaging**         | MassTransit (in-memory dev / RabbitMQ prod)          |
| **Validation**        | FluentValidation                                     |
| **Architecture Tests**| ArchUnitNET (NUnit)                                  |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio or Visual Studio Code
- SQL Server LocalDB (included with Visual Studio) for running services locally, or Docker Desktop for the full containerized stack (SQL Server 2022 + all four services)

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/ahmadmdabit/NotificationSystem.git
   cd NotificationSystem
   ```

2. Build the solution:
   ```bash
   dotnet build NotificationSystem.slnx
   ```

### Generating a Development Secret

The JWT signing secret is not stored in source control. Each developer must generate a local secret before first run.

**Option 1: User Secrets (recommended for development)**

```bash
# Same value in both services — they share one signing key
SECRET=$(openssl rand -hex 32)
(cd UserService && dotnet user-secrets init && dotnet user-secrets set "AppSettings:Secret" "$SECRET")
(cd NotificationService && dotnet user-secrets init && dotnet user-secrets set "AppSettings:Secret" "$SECRET")
(cd UI && dotnet user-secrets init && dotnet user-secrets set "ApiSettings:ServicePassword" "$(openssl rand -hex 16)")
```

**Option 2: Environment variables**

```bash
# PowerShell
$env:AppSettings__Secret = "$(openssl rand -hex 32)"

# bash
export AppSettings__Secret="$(openssl rand -hex 32)"
```

**Option 3: appsettings.Development.json (gitignored by default)**

Create `UserService/appsettings.Development.json`:

```json
{
  "AppSettings": {
    "Secret": "<64-char-hex-string>",
    "SqlConnectionString": "..."
  }
}
```

**Production:** Inject via environment variable or secret vault (Azure Key Vault, etc.). Never commit secrets.

**Verification:** After setting the secret, `dotnet run --environment Development` on UserService should not throw on `AppSettings.Secret`.

### Running the Application

**Option 1: Local development (no Docker)**

1. Generate and set the shared JWT signing secret plus the UI BFF password:

   ```bash
   export JWT_SECRET=$(openssl rand -hex 32)
   export AppSettings__Secret=$JWT_SECRET
   export ApiSettings__ServicePassword=$(openssl rand -hex 16)
   export SQL_SA_PASSWORD=ChangeMe@123!
   ```

   Or use User Secrets per project (see [Generating a Development Secret](#generating-a-development-secret)).

2. Start the microservices in separate terminals:
   ```bash
   cd UserService/UserService.Api && dotnet run
   cd NotificationService/NotificationService.Api && dotnet run
   cd ApiGateway && dotnet run
   cd UI && dotnet run
   ```

**Option 2: Docker Compose (recommended)**

1. Ensure Docker is available (this project assumes Docker inside WSL2 Ubuntu). Generate `.env` from `.env.example` and set secrets:

   ```bash
   cp .env.example .env
   # Edit .env: set SQL_SA_PASSWORD, JWT_SECRET, and UI_SERVICE_PASSWORD
   python3 -c "import secrets; print(secrets.token_hex(32))"
   ```

2. Start the full stack:

   ```bash
   docker compose up -d --build
   ```

3. Check health:
   ```bash
   docker compose ps  # all services should report (healthy)
   ```

**Option 3: Development watch**

```bash
cd UserService/UserService.Api && dotnet watch run
cd NotificationService/NotificationService.Api && dotnet watch run
cd ApiGateway && dotnet watch run
cd UI && dotnet watch run
```

| Service             | Host (Compose)                            | URL (local dev)           |
| ------------------- | ----------------------------------------- | ------------------------- |
| UserService         | `http://localhost:8081/api/Users`         | `https://localhost:44344` |
| NotificationService | `http://localhost:8081/api/Notifications` | `https://localhost:44314` |
| ApiGateway          | `http://localhost:8081`                   | `https://localhost:44315` |
| UI                  | `http://localhost:8080`                   | `https://localhost:44315` |
| SQL Server          | (not published — internal only)           | n/a                       |

## Project Structure

```
├── Common/                          # Shared helpers (ApiResult, ErrorResult, AppSettings)
├── Shared/
│   ├── Shared.Domain/               # DomainEvent, IRequest markers
│   ├── Shared.Application/          # IRequest, IDomainEventDispatcher
│   └── Shared.Infrastructure/       # InMemory/MassTransit event dispatchers, CommonBehavior
├── UserService/
│   ├── UserService.Domain/          # User entity, Password VO, UserRegisteredEvent, repository interfaces
│   ├── UserService.Application/     # CQRS commands/queries, DTOs, FluentValidation, behaviors
│   ├── UserService.Infrastructure/  # Dapper repositories, UnitOfWork, JwtTokenService, PasswordHasher, MassTransit
│   └── UserService.Api/             # Controllers, Program.cs, JWT auth
├── NotificationService/
│   ├── NotificationService.Domain/          # Notification entity, NotificationStatus VO, NotificationSentEvent
│   ├── NotificationService.Application/     # CQRS commands/queries, DTOs, FluentValidation, behaviors
│   ├── NotificationService.Infrastructure/  # Dapper repositories, TVP, UnitOfWork, MassTransit publisher
│   └── NotificationService.Api/             # Controllers, Program.cs, JWT auth
├── ApiGateway/                      # Ocelot API Gateway
├── UI/                              # ASP.NET Core 10 MVC (Bootstrap/jQuery/Grid.js)
│   └── Services/                    # GatewayApiClient, IGatewayApiClient - BFF pattern
└── tests/
    └── ArchitectureTests/           # ArchUnitNET dependency rule tests (NUnit, Debug only)
```

## Domain Entities

| Entity              | Location                                              | Key Fields                                      |
| ------------------- | ----------------------------------------------------- | ----------------------------------------------- |
| User                | `UserService/Domain/Entities/User.cs`                 | Id, Username, Password (VO), CreatedAt          |
| Notification        | `NotificationService/Domain/Entities/Notification.cs` | Id, Title, Content, Status (VO), SentAt        |
| NotificationHistory | `NotificationService/Domain/NotificationHistory.cs`   | NotificationId (PK), UserId (PK), CreatedAt     |

**NotificationHistory** is an infrastructure-level DTO (composite key, no domain behavior), not a domain entity or value object. It maps the join table for notification delivery tracking.

## API Documentation

Each microservice includes interactive Swagger documentation:

- **UserService**: `https://localhost:44344/swagger`
- **NotificationService**: `https://localhost:44314/swagger`

The documentation provides:

- Complete endpoint list
- Request/response schemas
- Interactive testing interface

## API Surface

All endpoints are async-only and authenticated by default (`[Authorize]` on `BaseApiController`). `Register` and `Authenticate` carry `[AllowAnonymous]`. `/health` is anonymous because it is not a controller action. The following endpoints are exposed beyond standard CRUD:

| Endpoint                                   | Method      | Description                                                      |
| ------------------------------------------ | ----------- | ---------------------------------------------------------------- |
| `/api/Users/Register`                      | POST        | Register a new user                                              |
| `/api/Users/Authenticate`                  | POST        | Authenticate and receive JWT                                     |
| `/api/Notifications/Send`                  | POST        | Send notifications to users (via stored procedure)               |
| `/api/[controller]/Bulk`                   | POST        | Bulk insert entities                                             |
| `/api/NotificationHistories/{key1}/{key2}` | GET, DELETE | Composite-key lookup/delete (two-part primary key)               |
| `/health`                                  | GET         | Liveness probe (anonymous; used by Docker Compose health checks) |

**Error Contract note:** error responses now use real HTTP status codes — `400` for bad input (e.g. duplicate username on Register), `401` for unauthenticated access. The JSON body shape (`success: false` + `ErrorResult`) is unchanged for clients that inspect `success`.

## Security

Authentication uses JWT tokens with the following characteristics:

- **Algorithm**: HMAC-SHA256
- **Expiry**: 7 days
- **Password Hashing**: PBKDF2 (`Rfc2898DeriveBytes`, HMAC-SHA512, 600,000 iterations, 256-bit salt) with constant-time verification (`CryptographicOperations.FixedTimeEquals`) in `UserService/Infrastructure/Services/PasswordHasher.cs`
- **Token Generation**: `JwtTokenService` in `UserService/Infrastructure/Services/JwtTokenService.cs` (implements `ITokenService` from Domain)
- **Token Validation**: All endpoints are authenticated by default via `[Authorize]`. `Register`, `Authenticate`, and `/health` remain anonymous. JWT validation is wired in both `UserService/Api/Program.cs` and `NotificationService/Api/Program.cs`.
- **Shared Secret**: Both services must use the same `AppSettings:Secret` value. Inject one `JWT_SECRET` (see [.env.example](.env.example)) — compose maps it into both services. Do not generate two keys.
- **Username Uniqueness**: A unique nonclustered index (`UXUsersUsername`) on `Users(Username)` enforces uniqueness at the database level, closing the check-then-act race on concurrent registration.
- **UI BFF**: The MVC UI never prompts for a user login. `UI/Services/GatewayApiClient` (implementing `IGatewayApiClient`, registered as singleton) registers/authenticates a service account (`ApiSettings:ServiceUsername` / `ServicePassword`, compose: `UI_SERVICE_PASSWORD`) and attaches an HTTP Authorization Bearer header on every gateway call. The client caches the token with an expiry timestamp (thundering-herd-safe refresh via `SemaphoreSlim` double-check), refreshes on 401, and disposes its `SemaphoreSlim` on shutdown.

## Database

- **Engine**: SQL Server (LocalDB for local dev, SQL Server 2022 container in Docker)
- **ORM**: Dapper 2.1.66 (pure Dapper — no EF Core)
- **Databases**: `UserDB` (UserService) and `NotificationDB` (NotificationService) — one per service, created on demand
- **Connection**: Configured via `AppSettings:SqlConnectionString` in each service's configuration. Local dev uses `Data Source=(LocalDB)\MSSQLLocalDB;Database=<DbName>;Integrated Security=True`; Docker injects `Server=sqlserver;Database=...;User Id=sa;Password=${SQL_SA_PASSWORD}` via `docker-compose.yml`
- **Database name**: always resolved from `Initial Catalog`/`Database` in the connection string — `DatabaseMigrationBase` reads it to create the database (reconnecting to `master` for the `CREATE DATABASE` step), so a connection string without an explicit catalog fails migration with `CREATE DATABASE []`
- **Schema**: Created automatically at service startup by `DatabaseMigration` (`IHostedService`), inheriting from `DatabaseMigrationBase` which handles database creation and retry logic. No external init scripts and no committed database files: `*.mdf` / `*.ldf` are ignored via [.gitignore](.gitignore)
- **Write operations**: Stored procedures (`sp_RegisterUser`, `sp_AuthenticateUser`, `sp_InsertNotification`, `sp_UpdateNotification`, `SPNotificationHistoryInsert`). Read operations use Dapper `QueryAsync`.

### Resetting the Databases

`DatabaseMigrationBase` only creates what does not exist yet (`IF NOT EXISTS` for databases, tables and types; `CREATE OR ALTER` for procedures), so a reset means dropping the databases and letting the services recreate them on the next start. **Always rebuild the images when resetting Docker** — the migration code ships inside the image, so booting a stale image against a wiped volume re-creates the stale schema.

> **Disk space matters on the Docker host.** Images and volumes live inside the WSL distro's virtual disk (e.g. `%LOCALAPPDATA%\Packages\CanonicalGroupLimited.Ubuntu_*\LocalState\ext4.vhdx`), which cannot grow once its host drive is full. Linux then remounts the filesystem **read-only**, so container health checks fail with `OCI runtime exec failed: ... read-only file system` and migrations fail with `operating system error 112 (There is not enough space on the disk)`. Free space (or run `docker system prune`) before a full reset.

**Docker — full reset (discards all data, including the SQL volume):**

```bash
docker compose down -v
docker compose up -d --build
```

**Docker — drop and recreate the databases only (keeps the SQL volume):**

```bash
docker compose stop userservice notificationservice
docker exec -it notificationsystem-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q \
  "ALTER DATABASE [UserDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [UserDB];
   ALTER DATABASE [NotificationDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [NotificationDB];"
docker compose up -d --build userservice notificationservice
```

`SINGLE_USER WITH ROLLBACK IMMEDIATE` is required to release the pooled connections held by the running services.

**Local dev — reset the LocalDB databases:**

```powershell
sqllocaldb stop MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "DROP DATABASE IF EXISTS UserDB; DROP DATABASE IF EXISTS NotificationDB;"
```

**Integration tests** — `docker-compose.test.yml` is throwaway (no named volume, and it declares its own Compose project name so `down -v` cannot touch the main stack): `docker compose -f docker-compose.test.yml down -v`.

**Verification** (after a reset, both databases must contain the current object names):

```bash
docker exec -e SQLCMDPASSWORD="$SQL_SA_PASSWORD" notificationsystem-sql \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d UserDB \
  -Q "SELECT name, is_unique FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Users');"

docker exec -e SQLCMDPASSWORD="$SQL_SA_PASSWORD" notificationsystem-sql \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d NotificationDB \
  -Q "SELECT name FROM sys.procedures; SELECT name FROM sys.types WHERE is_user_defined = 1;"
```

Expected: `UXUsersUsername` with `is_unique = 1` (plus the clustered PK) in `UserDB`, and `SPNotificationHistoryInsert` + `TypeNotificationHistory` in `NotificationDB`. Legacy names (`SP_NotificationHistory_I`, `Type_NotificationHistory`, missing `UXUsersUsername`) mean a stale image created the schema.

A reset must also be verified at the gateway, because a correct schema behind an unroutable gateway still fails every request:

```bash
docker exec notificationsystem-apigateway head -3 /app/ocelot.json    # must contain "Routes": [ (not "ReRoutes")
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8081/Users   # 401 = routed and authenticated, 404 = no route matched
```

`404` on every gateway path (with `UnableToFindDownstreamRouteError` in `docker logs notificationsystem-apigateway`) means the loaded config still uses the legacy `ReRoutes` key.

### Stored Procedures

The notification send operation uses `[dbo].[SPNotificationHistoryInsert]` with a Table-Valued Parameter of type `[dbo].[TypeNotificationHistory]`. The TVP is streamed via `TvpStreamingExtensions.AsSqlDataRecords()` with the column mapping defined in `NotificationService/Data/Tvp/NotificationHistoryTvpDefinition.cs` (single reusable `SqlDataRecord`, no `DataTable` materialization).

## Error Contract

All API errors return a standardized `ErrorResult` object:

```json
{
  "success": false,
  "error": {
    "code": 0,
    "message": "Description",
    "stackTrace": "...",
    "innerMessage": "...",
    "innerStackTrace": "..."
  }
}
```

Defined in `Common/Helpers/ErrorResult.cs`.

## Cross-cutting Concerns

- **CORS**: Pinned to the UI origin via `WithOrigins()` in each service (was `AllowAnyOrigin` — tightened to prevent direct cross-service access). The UI is a same-origin server-side proxy; browser clients never call the services directly.
- **Error Contract**: `ErrorResult` redacts `StackTrace`/`InnerStackTrace` in Production environments (details only in non-production), preventing stack-trace disclosure to API clients
- **MediatR Pipeline**: `ValidationBehavior` (FluentValidation), `LoggingBehavior`, and `TransactionBehavior` (wraps `ICommand` handlers in `UnitOfWork` transactions)

## Frontend Dependencies

- **RestSharp** (`114.0.0`): UI calls the API gateway via `GatewayApiClient` (`ApiSettings:GatewayBaseUrl`; Docker: `http://apigateway:8080`; local dev: `https://localhost:44315` in `UI/appsettings.json`)
- **Grid.js**: Loaded from CDN in `UI/Views/Home/Index.cshtml` for notification history display
- **Anti-forgery**: `[ValidateAntiForgeryToken]` on POST endpoints; token injected via `@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery`

## CI/CD

The project includes a GitHub Actions workflow (`.github/workflows/docker-build.yml`) that builds and pushes all four service images to Docker Hub on every push to `master`:

- `ahmadmdabit/notificationsystem-userservice`
- `ahmadmdabit/notificationsystem-notificationservice`
- `ahmadmdabit/notificationsystem-apigateway`
- `ahmadmdabit/notificationsystem-ui`

Each image is tagged with `latest` and the commit SHA. Build caching uses GitHub Actions cache (`type=gha`).

### Docker Compose Test Environment

`docker-compose.test.yml` provides an isolated, fail-fast SQL Server instance for integration tests. It uses `restart: "no"` and no named volume to ensure a clean database state on every run, and declares its own Compose project name (`notificationsystem-test`) so a `down -v` scoped to it cannot remove the main stack's containers or SQL volume. App services are intentionally excluded — tests connect to this SQL Server directly.

## Development

### Architecture Details

Clean Architecture + CQRS + DDD with the following layers per service:

1. **Domain**: Rich entities with behavior (`User.Create()`, `Notification.MarkAsSent()`), value objects (`Password`, `NotificationStatus`), domain events (`UserRegisteredEvent`, `NotificationSentEvent`), repository interfaces (`IUserRepository`, `INotificationRepository`), `IUnitOfWork`, `IDomainEventDispatcher`. No Dapper/EF attributes.
2. **Application**: CQRS commands/queries via MediatR (`IRequest<T>`), DTOs, FluentValidation, pipeline behaviors (validation, logging, transaction). `ICommand` marker for write operations.
3. **Infrastructure**: Dapper repositories (reads via `QueryAsync`, writes via stored procedures), `UnitOfWork`, `JwtTokenService`, `PasswordHasher`, MassTransit event publisher, TVP definitions.
4. **Api**: Controllers delegate to `IMediator`, JWT auth, `ApiResult<T>` response wrapper.

**Shared Kernel** (`Shared/`): `DomainEvent`, `IRequest`, `IDomainEventDispatcher`, in-memory and MassTransit dispatchers, `CommonBehavior` pipeline.

**Messaging**: Domain events dispatched via `IDomainEventDispatcher`. In-memory transport for local dev, RabbitMQ for Docker/prod (toggle via `Messaging:UseRabbitMQ`).

**Stored Procedures**: Write operations use SPs (`sp_RegisterUser`, `sp_AuthenticateUser`, `sp_InsertNotification`, `sp_UpdateNotification`, `SPNotificationHistoryInsert`). Reads use Dapper `QueryAsync`.

**Architecture Tests**: ArchUnitNET (NUnit, Debug config only) enforces dependency direction rules. Run: `dotnet test tests/ArchitectureTests/ArchitectureTests.csproj -c Debug`.

### Adding New Features

1. Define the entity in the service's `Domain/Entities/` folder
2. Create repository interface in `Domain/Abstractions/`
3. Implement concrete repository in `Infrastructure/Repositories/`
4. Create DTO in `Application/DTOs/`
5. Create command/query in `Application/Commands/` or `Application/Queries/`
6. Create handler in same directory, implement `IRequestHandler<TCommand, TResponse>`
7. Create FluentValidation validator in same directory
8. Add controller in `Api/Controllers/`, delegate to `IMediator`
9. Register services in `Api/Program.cs`
10. Add routing in `ApiGateway/ocelot.json` **under the `Routes` array** (single source; `ocelot.Development.json` overrides for local dev). Do not use the legacy `ReRoutes` key — Ocelot 25.x ignores it and every gateway call returns 404
11. Create UI pages if needed

## License

Licensed under the [MIT license](LICENSE.md).
