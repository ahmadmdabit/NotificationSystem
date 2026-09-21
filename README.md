# NotificationSystem

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/ahmadmdabit/NotificationSystem)
[![.NET](https://img.shields.io/badge/.NET-Core_3.1-blue)](https://dotnet.microsoft.com/)
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

A modern, scalable notification system built with .NET Core using a microservices architecture. This project demonstrates best practices in software design, including separation of concerns, clean architecture, and API gateway pattern implementation.

The application consists of multiple components:

- **Microservices**: UserService and NotificationService for handling business logic
- **API Gateway**: Centralized routing using Ocelot
- **Web UI**: MVC application with responsive design
- **Shared Libraries**: Common, DAL, BLL, and API layers for code reuse

## Key Features

- **Notification Management**: Create, send, and track notifications
- **User Management**: User registration and profile management
- **Microservices Architecture**: Independent, scalable services
- **API Gateway**: Centralized request routing and management
- **API Documentation**: Interactive Swagger UI for all services
- **Responsive UI**: Modern web interface using Bootstrap and jQuery
- **Local Database**: SQL Server LocalDB with Dapper ORM
- **CORS Support**: Cross-origin resource sharing enabled
- **JWT Authentication**: HMAC-SHA256 token-based auth with 7-day expiry, validated by `JwtBearer` in both services. See [Security](#security).
- **Authorization**: `[Authorize]` on `BaseApiController` — all endpoints are authenticated by default except `Register`, `Authenticate`, and `/health` (`[AllowAnonymous]`)
- **Docker Containerization**: Multi-stage alpine images, non-root execution, health checks, and app-level schema migration via `IHostedService`

## Architecture Diagram

[![Interactive Diagram](https://raster.shields.io/badge/Interactive_Diagram-lightgreen.png?logoColor=eeeeee&logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAMAAAAoLQ9TAAAAzFBMVEUAAACTM+qTM+mTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+qTM+pYr7W1AAAAQ3RSTlMAAAAlZGhpWxQEBajeV3QHCsHcYO6ABgm/3V75/oTtnJ7TVIqWjivDzJWXcs8cy8CHbPvrwqIIXQHKJyiZJinO0P3jWa9vVAAAAKRJREFUGNNVz+kSgiAYhWFI2zRTWtCKtJ2sLG3fM7n/ewqBpun9+czwzQEA+AuIClDTi6VypfoVaJiMsZpVt5VAB3FoNFttLAW6HodOt0f6jgLkB4PhaDyZzhRQatvePAwXS6xgFUVr/oxRR8KGxTHJwXMVkCTZ7oLARwr2B3w8WWdMqQJ0ud7M++P5UsCHpSl7ZxlB8qiYLjINAfnnRLomh/0FPrSFFcj8a3ouAAAAAElFTkSuQmCC)](https://gitdiagram.com/ahmadmdabit/NotificationSystem)

![The project's diagram](ahmadmdabit-notificationsystem-diagram.png)

## Tech Stack

| Layer                 | Technology                                           |
| --------------------- | ---------------------------------------------------- |
| **Framework**         | ASP.NET Core 3.1                                     |
| **Architecture**      | Microservices with API Gateway                       |
| **Languages**         | C#                                                   |
| **Database**          | SQL Server LocalDB + Dapper ORM                      |
| **API Gateway**       | Ocelot 15.0.7                                        |
| **Container**         | Docker (multi-stage alpine, non-root, health checks) |
| **Frontend**          | ASP.NET Core MVC (Bootstrap, jQuery, Grid.js)        |
| **API Documentation** | Swagger/OpenAPI                                      |
| **HTTP Client**       | RestSharp                                            |

## Getting Started

### Prerequisites

- [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1)
- Visual Studio or Visual Studio Code
- SQL Server LocalDB (included with Visual Studio)

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/ahmadmdabit/NotificationSystem.git
   cd NotificationSystem
   ```

2. Build the solution:
   ```bash
   dotnet build
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
   cd UserService && dotnet run
   cd NotificationService && dotnet run
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
   docker compose ps  # all 5 services should report (healthy)
   ```

**Option 3: Development watch**

```bash
cd UserService && dotnet watch run
cd NotificationService && dotnet watch run
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
├── Common/
│   └── Common Library (.NET Standard 2.0) - Helpers, Extensions
├── DAL/
│   └── DAL Library (.NET Standard 2.0) - Dapper & SQL Server (base classes, DatabaseMigrationBase)
├── BLL/
│   └── BLL Library (.NET Standard 2.0) - Business logic interfaces & base
├── API/
│   └── API Library (.NET Standard 2.0) - Swagger, base controllers
├── Services/
│   ├── UserService (ASP.NET Core 3.1 RESTful API)
│   └── NotificationService (ASP.NET Core 3.1 RESTful API)
├── ApiGateway/ (Ocelot API Gateway)
└── UI/ (ASP.NET Core 3.1 MVC - Bootstrap/jQuery/Grid.js)
    └── Services/ (GatewayApiClient, IGatewayApiClient - BFF pattern)
```

## Domain Entities

| Entity              | Location                                              | Key Fields                                      |
| ------------------- | ----------------------------------------------------- | ----------------------------------------------- |
| User                | `UserService/Entities/User.cs`                        | Id, Username, PasswordHash, PasswordSalt, Token |
| Notification        | `NotificationService/Entities/Notification.cs`        | Id, Title, Content                              |
| NotificationHistory | `NotificationService/Entities/NotificationHistory.cs` | NotificationId (PK), UserId (PK), CreatedAt     |

**NotificationHistory** uses a composite key (`NotificationId` + `UserId`) to track which users have received which notifications.

## API Documentation

Each microservice includes interactive Swagger documentation:

- **UserService**: `https://localhost:44344/swagger`
- **NotificationService**: `https://localhost:44314/swagger`

The documentation provides:

- Complete endpoint list
- Request/response schemas
- Interactive testing interface

## API Surface

All endpoints are async-only and authenticated by default (`[Authorize]` on `BaseApiController`). `Register` and `Authenticate` carry `[AllowAnonymous]`. `/health` is anonymous because it is not a controller action (no FallbackPolicy in 3.1). The following endpoints are exposed beyond standard CRUD:

| Endpoint                  | Method | Description                                                      |
| ------------------------- | ------ | ---------------------------------------------------------------- |
| `/api/Users/Register`     | POST   | Register a new user                                              |
| `/api/Users/Authenticate` | POST   | Authenticate and receive JWT                                     |
| `/api/Notifications/Send` | POST   | Send notifications to users (via stored procedure)               |
| `/api/[controller]/Bulk`  | POST   | Bulk insert entities                                             |
| `/health`                 | GET    | Liveness probe (anonymous; used by Docker Compose health checks) |

**Error Contract note:** error responses now use real HTTP status codes — `400` for bad input (e.g. duplicate username on Register), `401` for unauthenticated access. The JSON body shape (`success: false` + `ErrorResult`) is unchanged for clients that inspect `success`.

## Security

Authentication uses JWT tokens with the following characteristics:

- **Algorithm**: HMAC-SHA256
- **Expiry**: 7 days
- **Token Generation**: `UsersController.TokenGenerate()` in `UserService/Controllers/UsersController.cs`
- **Token Validation**: All endpoints are authenticated by default (`[Authorize]` on `BaseApiController` in `API/Controller/BaseApiController.cs`). `Register`, `Authenticate`, and `/health` remain anonymous. JWT validation is wired in both `UserService/Startup.cs` and `NotificationService/Startup.cs`.
- **Shared Secret**: Both services must use the same `AppSettings:Secret` value. Inject one `JWT_SECRET` (see [.env.example](.env.example)) — compose maps it into both services. Do not generate two keys.
- **UI BFF**: The MVC UI never prompts for a user login. `UI/Services/GatewayApiClient` (implementing `IGatewayApiClient`, registered as singleton) registers/authenticates a service account (`ApiSettings:ServiceUsername` / `ServicePassword`, compose: `UI_SERVICE_PASSWORD`) and attaches an HTTP Authorization Bearer header on every gateway call. The client caches the token, refreshes on 401, and disposes its `SemaphoreSlim` on shutdown.

## Database

- **Engine**: SQL Server (LocalDB for local dev, SQL Server 2022 container in Docker)
- **ORM**: Dapper 2.0.90
- **Connection**: Configured via `AppSettings.SqlConnectionString` in each service's `appsettings.json` (local dev uses `|DataDirectory|` token via `appsettings.Development.json`; Docker injects `Server=sqlserver;Database=...;User Id=sa;...` via `docker-compose.yml`)
- **Schema**: Created automatically at service startup by `DatabaseMigration` (`IHostedService` in `UserService/Startup.cs` / `NotificationService/Startup.cs`), inheriting from `DAL.DatabaseMigrationBase` which handles database creation and retry logic. No external init scripts or committed `.mdf` files.

### Stored Procedures

The notification send operation uses `[dbo].[SP_NotificationHistory_I]` with a Table-Valued Parameter of type `[dbo].[Type_NotificationHistory]`. The TVP is constructed via `IEnumerableExtensions.ToDataTable()` in `NotificationService/Businesses/NotificationBusiness.cs`.

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

- **IP Logging**: `IActionContextAccessor` captures client IP in `BaseApiController` (`API/Controller/BaseApiController.cs`)
- **CORS**: Pinned to the UI origin via `WithOrigins()` in each service `Startup.cs` (was `AllowAnyOrigin` — tightened to prevent direct cross-service access). The UI is a same-origin server-side proxy; browser clients never call the services directly.

## Frontend Dependencies

- **RestSharp** (`106.12.0`): UI calls the API gateway via `GatewayApiClient` (`ApiSettings:GatewayBaseUrl`; Docker: `http://apigateway:8080`; local dev: `https://localhost:44315` in `UI/appsettings.json`)
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

`docker-compose.test.yml` provides an isolated, fail-fast SQL Server instance for integration tests. It uses `restart: "no"` and no named volume to ensure a clean database state on every run. App services are intentionally excluded — tests connect to this SQL Server directly.

## Development

### Architecture Details

This N-Tier architecture uses a **layered diaspora** pattern:

1. **Common Layer**: Shared helpers (`ApiResult<T>`, `ErrorResult`, `AppSettings`) and extensions (`IEnumerableExtensions`, `ReflectionExtensions`)
2. **DAL Layer**: `IRepository<T>` interface and `BaseRepository<T>` abstract class with Dapper CRUD + stored procedure support; `DatabaseMigrationBase` abstract class for schema creation
3. **BLL Layer**: `IBusiness<T>` interface and `BaseBusiness<T>` abstract class
4. **API Layer**: `IApiController<T>` interface and `BaseApiController<T>` abstract class with standardized CRUD endpoints (authenticated by default via `[Authorize]`), `DatabaseMigration` hosted services for schema creation. Concrete implementations (entities, repositories, business classes, controllers) reside in the service projects that use them.
5. **Services**: Concrete entities, repositories, business logic, and controllers live in `UserService/` and `NotificationService/`
6. **ApiGateway**: Ocelot routing via `ocelot.json`
7. **UI**: MVC frontend calling the gateway via RestSharp

The shared libraries contain only base classes and interfaces. Concrete implementations (entities, repositories, business classes, controllers) reside in the service projects that use them.

### Adding New Features

1. Define the entity in the service's `Entities/` folder (e.g., `NotificationService/Entities/`)
2. Create repository interface in `DAL/Repository/` (if new generic contract needed)
3. Implement concrete repository in the service's `Repositories/` folder
4. Create business interface in `BLL/Business/` (if new generic contract needed)
5. Implement concrete business class in the service's `Businesses/` folder
6. Add controller in the service's `Controllers/` folder (inherit from `BaseApiController<T>`)
7. Register services in the microservice's `Startup.cs`
8. Add routing in `ApiGateway/ocelot.json` (single source; `ocelot.Development.json` overrides for local dev)
9. Create UI pages if needed

## License

Licensed under the [MIT license](LICENSE.md).
