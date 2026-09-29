# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Goal

**Thing-Tag** is an end-to-end tracking system that follows the movement of any item from origin to destination, giving the kind of visibility a professional logistics network provides.

> Thing-Tag คือระบบติดตามอัจฉริยะที่ออกแบบมาเพื่อเกาะติดทุกการเคลื่อนไหวของสิ่งของทุกประเภท ตั้งแต่ต้นทางจนถึงปลายทางอย่างไร้รอยต่อ มอบประสบการณ์การขนส่งที่โปร่งใสและตรวจสอบได้จริง เหมือนกับระบบโลจิสติกส์มาตรฐานระดับสากล

Built so far: auth and permissions; master data; tagged items; append-only tracking events; containers/consolidation; and multimodal shipments (road, rail, air, sea and courier legs, with customs). The legacy `Products` CRUD predates Thing-Tag. For now this project is purely the backend REST API for a separate frontend app, so it serves JSON only (no server-rendered UI). New features should build toward that goal and reuse the existing layering and permission model.

## Commands

```bash
dotnet restore
dotnet build
dotnet run                      # http://localhost:5106 (profile "http")
dotnet run --launch-profile https   # https://localhost:7195
```

- Swagger UI (`/swagger`) and OpenAPI (`/openapi/v1.json`) are only mapped in the Development environment. Swagger has a Bearer "Authorize" button; get a token from `POST /api/Auth/login`.
- Database schema is **not** managed by EF Core migrations. It's created from hand-written SQL scripts:
  ```bash
  psql -h localhost -p 5432 -U postgres -d myfirstapi_db -f Scripts/run_all.sql
  ```
- There is no automated test project yet. API tests live in a **Bruno** collection in `bruno/`: open the folder in the Bruno app, pick the `Local` environment, and run `01 Auth/Login` or the whole collection with the Runner.
  - Requests are chained through runtime variables (`token`, `categoryId`, `locationId`, `partyId`, `itemId`, ...).
  - Codes include a per-run `runId`, so the collection can be re-run against the same database.
  - Folders run in order and build on each other. For example, Shipments uses the locations, parties, carriers, vehicles, items and container created earlier. The Login script also generates a valid per-run ISO 6346 container number (`isoContainerCode`).
  - Prefer declarative `assert` lines (`res.body.x: eq "..."`) over JS `tests`, and use JS only for things asserts can't express.
  - When adding or changing an endpoint, add or update its `.bru` requests in the matching numbered folder, following the same `assert` / `script:post-response` style.

## Architecture

ASP.NET Core 10 controller-based Web API, PostgreSQL via EF Core (Npgsql), JWT bearer auth. Layering: `Controllers/` → `Interfaces/` + `Services/` (registered as scoped in `Program.cs`) → `Data/AppDbContext.cs` → PostgreSQL. The older `ProductsController` binds directly to `Models/` entities. Thing-Tag features follow a stricter pattern, with `TrackedItemsController` as the reference:
- Request/response DTOs live in `Dtos/`. Validation uses DataAnnotations/`IValidatableObject`, and `[ApiController]` returns 400 ProblemDetails automatically. Services map entities to response DTOs, so entities are never returned directly.
- Services throw `Exceptions/ConflictException` for data clashes and `Exceptions/BusinessRuleException` for business-rule failures. `Exceptions/ApiExceptionHandler` (registered in `Program.cs`) turns these into `409` and `400` responses shaped as `{ message }`, so controllers have no try/catch.
- Enums are serialized as strings (`[JsonConverter(typeof(JsonStringEnumConverter<T>))]` on the enum) and stored as strings (`HasConversion<string>()`).
- Lists are paged with `PagedResult<T>`. Items are archived (`IsArchived`) instead of deleted so tracking history survives.
- **Master data** (`ItemCategories`, `Locations`, `Parties`, `EventTypes`, `ReasonCodes`, `Carriers`, `Vehicles`, `Containers`) is built on shared generic bases: `Services/MasterDataService<...>` and `Controllers/MasterDataController<...>`.
  - Each concrete service only defines its DbSet, projection, search, filters, order and `Apply`. Each concrete controller only adds `[Route]`.
  - Entities implement `Models/IMasterData`, and responses implement `IMasterDataResponse`.
  - Codes are unique and stored upper-case. `DELETE` deactivates (`IsActive=false`) instead of deleting.
  - An inactive row stays valid on records that already reference it, but can't be newly assigned (`Services/ReferenceResolver`).
  - Async cross-checks go in the `ValidateAsync` hook, which runs before `Apply` so the entity still holds its old values.
  - To add a new master table: add an entity, DTOs, a service, an interface, a controller, a SQL script, a DbSet and a DI registration.
- `TrackedItem` optionally references `ItemCategory`, `Location` (`CurrentLocationId`), `Party` (owner) and `Container` (`CurrentContainerId`), all with FK `ON DELETE RESTRICT`. A category's `RequiredAttributes` keys must be present in the item's `Attributes`.
- Timestamps are UTC `DateTime` stored as `timestamptz`. Request timestamps are `DateTimeOffset`, converted with `.UtcDateTime`. The current username comes from `ICurrentUser` (the JWT `sub` claim, which JwtBearer maps to `ClaimTypes.NameIdentifier`).

### Tracking model (the core of Thing-Tag)

- **State only changes through events.** An item's `Status` and `CurrentLocationId` are never edited directly: `PUT` doesn't touch them, and there is no status endpoint. Every change is a `TrackingEvent`, a row that is never updated or deleted.
  - `EventType.ResultingStatus` decides the new status (null means the event is informational).
  - An event only updates the item if its `OccurredAt >= item.LastEventAt`. A back-dated event is kept in the history without rewinding the item.
- **Mistakes are voided, not deleted.**
  - `POST /api/TrackingEvents/{id}/void` flags the event (`IsVoided`, with who/when/why). `/correct` voids it and records a replacement linked by `ReplacesEventId`.
  - Both call `ITrackingEventRecorder.RecalculateItemStateAsync`, which rebuilds the item's status and location from its remaining non-voided events, including unsaved ones in the change tracker.
  - Voided events are hidden from lists unless `includeVoided=true`.
  - Events that mirror a shipment or container operation (`IsSystemManaged`: leg depart/arrive, customs, delivery, container load/unload) can't be voided. They must be undone through that operation, so shipment and container state stay consistent.
- **Reason codes.** An `EventType` with `RequiresReason` (for example `DELIVERY_FAILED`, `DAMAGED`, `LOST`, `RETURNED`, `CUSTOMS_HOLD`) must be recorded with a `reasonCode`.
  - The recorder checks that the code exists and is active, that it applies to the event type (`ReasonCode.EventTypeCodes`, where empty means any), and that a note is present when `RequiresNote` is set.
- **`ITrackingEventRecorder`** (`Services/TrackingEventRecorder.cs`) is the single place that records events. It is used by `TrackingEventService` (single event or bulk scan), `TrackedItemService` (auto `REGISTERED` on create), `ContainerService` and `ShipmentService`.
  - It adds entities but never calls `SaveChanges`, so each caller commits its own changes and the events in one transaction.
  - It also resolves items by id or tag (rejecting archived ones) and expands nested container trees.
- **Event codes the code relies on:** some services look up event types by code, so these seeded codes must exist:
  - `REGISTERED`, `DELIVERED`, `CUSTOMS_HOLD`, `CUSTOMS_CLEARED`
  - `VEHICLE_/TRAIN_/FLIGHT_/VESSEL_DEPARTED` and the matching `_ARRIVED` codes
  - `LOADED_INTO_CONTAINER`, `UNLOADED_FROM_CONTAINER`
  - See `Scripts/011` and `Scripts/021`, and the constants in `ShipmentService` and `ContainerService`.
- **Containers** nest through `ParentContainerId` (item → pallet → container). Scanning a container records the event for every item in the whole tree and moves the nested containers with it. Codes of ISO container types must pass the ISO 6346 check digit (`Services/Iso6346.cs`).
- **Shipments** have ordered `ShipmentLegs`, one mode, carrier and vehicle per leg.
  - Leg rules are enforced in `ShipmentService.ApplyLegsAsync` and `ValidateTransportAsync`:
    - legs must chain from origin to destination;
    - a vehicle or carrier must match the leg's mode (Road and Courier count as one family);
    - the document type defaults from the mode (B/L, AWB, ...).
  - The lifecycle is `Planned → InTransit → Delivered`, or `Planned → Cancelled`:
    - depart/arrive endpoints set ATD/ATA and record mode-specific events for every item;
    - a customs `Hold` blocks departure and delivery;
    - international shipments (origin and destination in different countries) start with customs `Pending`.
  - An item can be in only one open (Planned or InTransit) shipment at a time. A full `PUT` is only allowed while the shipment is Planned; after that, only a leg that hasn't departed can be edited.
- `Dictionary` properties map to `jsonb`, which relies on `EnableDynamicJson()` in the `UseNpgsql` setup in `Program.cs`.

The dev PostgreSQL database runs in a Docker container named `postgres-server`, and `psql` isn't installed on the host. Apply a script with `docker exec -i postgres-server psql -U postgres -d myfirstapi_db -v ON_ERROR_STOP=1 < Scripts/NNN_x.sql`. The seeded login is `admin` / `ChangeMe123!` (see `Scripts/003_users_seed_admin.sql`).

### Schema changes

`Scripts/` holds numbered, idempotent SQL files (`CREATE TABLE IF NOT EXISTS`, `ON CONFLICT DO NOTHING`), one per feature, schema before seed. When adding a table/column:
1. Add a new `NNN_*.sql` file (next number) and add an `\ir` line for it in `Scripts/run_all.sql`.
2. Update the entity in `Models/` and `DbSet`/`OnModelCreating` in `AppDbContext` to match. The two must be kept in sync manually.

### Authorization (database-driven permissions)

This spans several files and is the main non-obvious design:

- **Secure by default**: `Program.cs` sets a fallback policy requiring an authenticated user. Endpoints must opt out with `[AllowAnonymous]` (only `AuthController.Login` does).
- Actions use `[Authorize(Policy = Permissions.X)]` with constants from `Authorization/Permissions.cs` (e.g. `"Products.Read"`).
- `Authorization/PermissionPolicyProvider.cs` (custom `IAuthorizationPolicyProvider`) turns *any* policy name into a `PermissionRequirement` — policies are never registered individually in `Program.cs`.
- `Authorization/PermissionAuthorizationHandler.cs` checks the JWT's role claim(s) against the `RolePermissions` table (joined to `Permissions.Code`) via `AppDbContext` on every request.
- `Users.Role` is free text; roles are not an enum. JWT is issued in `Services/AuthService.cs` with a `ClaimTypes.Role` claim, 2-hour expiry, and passwords hashed with BCrypt.

**Current phase:** new Thing-Tag features are being built without permission policies. They rely only on the fallback "authenticated user" policy. Per-endpoint permissions will be added for all features together once they're all built. Until then, don't add `Permissions.cs` constants, `[Authorize(Policy = ...)]` attributes or permission seed scripts for new features.

Adding a new protected endpoint (once permissions are being applied) means: add a constant to `Permissions.cs`, decorate the action, and add a SQL script that inserts the `Permissions` row and the `RolePermissions` grants (see `Scripts/005_permissions_seed.sql`). Granting/revoking access for an existing permission is purely a data change.

### Configuration

`appsettings.json` holds `ConnectionStrings:DefaultConnection` and `Jwt:Key`/`Issuer`/`Audience`, read both in `Program.cs` (validation) and `AuthService` (token signing).

## Repo notes

- `bin/` and `obj/` are tracked in git (no `.gitignore`), so builds produce noisy diffs. Don't stage them unless asked.
- Package versions are mixed: .NET 10 / ASP.NET Core 10.x packages alongside EF Core 9.x and Npgsql 9.x.
- Some comments in `Program.cs` are in Thai.
- Commit messages follow a Conventional Commits-like `type : message` style (e.g. `feat : Auth`, `refactor : README`).
