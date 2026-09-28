# Database

[Back to README](../README.md)

- **Engine**: SQL Server (LocalDB for local dev, SQL Server 2022 container in Docker)
- **ORM**: Dapper 2.1.86 (pure Dapper — no EF Core)
- **Databases**: `UserDB` (UserService) and `NotificationDB` (NotificationService) — one per service, created on demand
- **Connection**: Configured via `AppSettings:SqlConnectionString` in each service's configuration. Local dev uses `Data Source=(LocalDB)\MSSQLLocalDB;Database=<DbName>;Integrated Security=True`; Docker injects `Server=sqlserver;Database=...;User Id=sa;Password=${SQL_SA_PASSWORD}` via `docker-compose.yml`
- **Database name**: always resolved from `Initial Catalog`/`Database` in the connection string — `DatabaseMigrationBase` reads it to create the database (reconnecting to `master` for the `CREATE DATABASE` step), so a connection string without an explicit catalog fails migration with `CREATE DATABASE []`
- **Schema**: Created automatically at service startup by `DatabaseMigration` (`IHostedService`), inheriting from `DatabaseMigrationBase` which handles database creation and retry logic. No external init scripts and no committed database files: `*.mdf` / `*.ldf` are ignored via [.gitignore](../.gitignore)
- **Write operations**: Stored procedures (`SPRegisterUser`, `SPAuthenticateUser`, `SPInsertNotification`, `SPUpdateNotification`, `SPNotificationHistoryInsert`). Read operations use Dapper `QueryAsync`.
- **SQL contract**: repositories only _execute_; the `CommandDefinition`s are built in `Persistence/UserCommandFactory.cs` and `Persistence/NotificationCommandFactory.cs`, with the shapes common to both services in `Shared.Infrastructure/Persistence/SqlCommands.cs`. Dapper's query methods are static extensions on `IDbConnection`, so the commands — not the connection — are the testable unit. See [Testing](testing.md#testing).

## Resetting the Databases

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

## Stored Procedures

The notification send operation uses `[dbo].[SPNotificationHistoryInsert]` with a Table-Valued Parameter of type `[dbo].[TypeNotificationHistory]`. The TVP is streamed via `TvpStreamingExtensions.AsSqlDataRecords()` with the column mapping defined in `Services/NotificationService/NotificationService.Infrastructure/Data/Tvp/NotificationHistoryTvpDefinition.cs` (single reusable `SqlDataRecord`, no `DataTable` materialization).

> ⚠️ **The buffer is shared across the whole sequence** — that is the zero-allocation trade. Every element yielded is the _same_ `SqlDataRecord`, so the sequence must be enumerated lazily, which is what `AsTableValuedParameter` does. Materialising it (`ToList()`/`ToArray()`) returns the right number of rows with every row holding the **last** entity's values, and raises nothing. `TvpStreamingTests` pins both the streaming behaviour and the aliasing contract.

> ✅ **The SP insert is idempotent.** `NotificationHistories` has `PRIMARY KEY (NotificationId, UserId)`, so the insert is filtered with `WHERE NOT EXISTS` against existing rows. Sending the same `(notification, user)` pair twice inserts nothing the second time and still reports `@SPSuccess = 1` — without that guard the second send raised a PK violation, which the SP reported as a failure and the repository surfaced as HTTP 500. Soft-deleted rows still occupy the key, so the guard covers them too. `StoredProcedureContractTests.Sp_NotificationHistoryInsert_IsIdempotent_For_A_RepeatPair` pins the guard **as text**; a behavioural proof needs a live database.
