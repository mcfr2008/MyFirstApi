# Thing-Tag API

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-14%2B-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![OpenAPI](https://img.shields.io/badge/OpenAPI-Swagger-85EA2D?logo=swagger&logoColor=black)](https://swagger.io/specification/)
[![License](https://img.shields.io/badge/license-TBD-lightgrey)](#license)

> **Thing-Tag** is an intelligent end-to-end tracking system that follows the movement of any item from origin to destination, with the visibility of a professional logistics network.
>
> **Thing-Tag** คือระบบติดตามอัจฉริยะที่ออกแบบมาเพื่อเกาะติดทุกการเคลื่อนไหวของสิ่งของทุกประเภท ตั้งแต่ต้นทางจนถึงปลายทางอย่างไร้รอยต่อ มอบประสบการณ์การขนส่งที่โปร่งใสและตรวจสอบได้จริง เหมือนกับระบบโลจิสติกส์มาตรฐานระดับสากล

This repository (`MyFirstApi`) is the **backend REST API** for Thing-Tag, built with **ASP.NET Core 10**, **Entity Framework Core** and **PostgreSQL**. It serves JSON only; a separate frontend consumes it.

## Features

- **Tagged items**
  - Register items with a QR, barcode or RFID tag code, or let the API generate one (`TT-XXXXXXXXXX`).
  - Register up to 500 items at once, all-or-nothing.
  - Store category-specific attributes and customs data (HS code, country of origin, currency).
  - Archive instead of delete, so history is kept.
- **Master data**
  - Item categories, including dangerous goods (UN number, hazard class).
  - Locations: warehouses, hubs, ports, airports, rail stations, with UN/LOCODE, IATA code and time zone.
  - Parties, event types, reason codes, carriers, vehicles and containers.
- **Tracking events**
  - An item's status and location change **only** through events: a single record or a bulk scan of many tags at once.
  - Full timelines per item.
  - Wrong events are **voided or corrected** with an audit trail, and the item's state is recalculated.
  - Exception events (failed delivery, damage, loss, customs hold) require a **reason code**.
- **Containers and consolidation**
  - Nest handling units: item → pallet → container.
  - ISO 6346 container-number validation.
  - Load and unload, and one scan of a container records the event for everything inside.
- **Multimodal shipments**
  - Shipments move over ordered **legs** by road, rail, air, sea or courier.
  - Legs record carrier, vehicle, voyage/flight number, ETD/ETA vs ATD/ATA with delay calculation, and transport documents (B/L, AWB, ...).
  - Incoterms and customs hold/clear.
  - Every departure, arrival and delivery records events for all items in the shipment.
- **Proof of delivery**
  - The receiver **signs on the courier's device**, optionally with photos. The API stores the signature image with its SHA-256, the receiver's name and relation, GPS and time.
  - The same request delivers the shipment. Items the receiver refuses get `DELIVERY_FAILED` with a reason instead.
  - Shipments require a signature by default.
- **Safe retries (idempotency keys)**
  - Scans, events, container and shipment operations and item creation accept an `Idempotency-Key` header. A retry after a lost connection returns the first response instead of recording twice.
- **Public tracking**
  - Anyone with the tracking number can follow a shipment **without logging in**: status, route legs with ETD/ETA and ATD/ATA, ETA, and a newest-first timeline.
  - Only public details are shown, and requests are rate limited per IP (see [Public tracking](#public-tracking)).
- **Built for growth**
  - Monthly-partitioned history with partitions created ahead automatically.
  - Cursor paging, trigram search indexes and a master-data cache.
  - Preventive-maintenance tooling (see [Performance and maintenance](#performance-and-maintenance)).
- **Security**
  - JWT authentication and database-driven, permission-based authorization.
- **Self-documenting database**
  - Every table and column has an `English | ภาษาไทย` comment, visible in DBeaver or pgAdmin.

## Architecture

```text
Client (frontend, scanner app)
        │  JSON over HTTPS, JWT bearer
        ▼
Controllers            ← HTTP endpoints, DTO validation (400 ProblemDetails)
        ▼
Services / Interfaces  ← business rules; errors → ProblemDetails { code, args }
        ▼
Entity Framework Core  ← AppDbContext
        ▼
PostgreSQL             ← schema from numbered SQL scripts (no EF migrations)
```

| Folder | Responsibility |
|---|---|
| `Controllers/` | HTTP endpoints. Master data shares one generic `MasterDataController`. |
| `Services/` | Business logic. Master data shares `MasterDataService`. `TrackingEventRecorder` is the single place that records events. |
| `Interfaces/` | Service contracts |
| `Dtos/` | Request/response models and validation. Entities are never returned directly. |
| `Models/` | EF Core entities and enums |
| `Exceptions/` | Error catalog (`Errors.cs`: stable codes with English and Thai templates), `ApiException`, and `ApiExceptionHandler` that maps them to ProblemDetails |
| `Authorization/` | Permission policy provider and handler |
| `Data/` | `AppDbContext` |
| `Scripts/` | Numbered SQL scripts; also `maintenance/` and `dev/` tools |
| `bruno/` | Bruno API test collection |

### Tracking model

```text
TrackedItem ──< TrackingEvent >── EventType (→ ResultingStatus)
     │               │  └──────── ReasonCode (exceptions)
     │               └─ Location, Shipment/Leg, Container
     ├── CurrentLocation / Status / LastEventAt   (current state, updated by events)
     └── CurrentContainer ── Container ── ParentContainer ...

Shipment ──< ShipmentLeg (Road → Sea → Road ...)
     └────< ShipmentItem >── TrackedItem
```

- **Current state is stored on the item**, so "where is it now?" never reads history.
- **Events are never edited or deleted.** A void keeps the original, flags it and recalculates the item.
- **An event older than the item's latest one** is kept in history but doesn't rewind the item.
- **Events recorded by a shipment or container operation** are undone through that operation (for example, unload the container), not voided directly.

## API overview

All routes are **versioned**: `/api/v1/...`. Breaking changes will go into `/api/v2` so existing apps keep working. Every endpoint except login, the error catalog and public tracking requires `Authorization: Bearer <token>`. Swagger UI is at `/swagger` in Development.

| Area | Endpoints |
|---|---|
| Auth | `POST /api/v1/Auth/login` · `POST /api/v1/Auth/register` |
| Error catalog | `GET /api/v1/ErrorCodes`: every error code with English and Thai message templates (no login needed) |
| Tracked items | `GET/POST /api/v1/TrackedItems` · `POST /bulk` · `GET/PUT/DELETE /{id}` · `POST /{id}/restore` · `GET /by-tag/{tagCode}` |
| Tracking events | `GET/POST /api/v1/TrackingEvents` · `POST /scan` · `GET /{id}` · `POST /{id}/void` · `POST /{id}/correct` |
| Containers | master-data endpoints (below) + `GET /{id}/contents` · `POST /{id}/load` · `/unload` · `/scan` |
| Shipments | `GET/POST /api/v1/Shipments` · `GET/PUT /{id}` · `GET /by-tracking/{no}` · `PUT /{id}/legs/{legId}` · `POST /{id}/legs/{legId}/depart` · `/arrive` · `GET/POST /{id}/items` · `DELETE /{id}/items/{itemId}` · `POST /{id}/customs` · `/events` · `/deliver` · `/cancel` |
| Proof of delivery | `POST /api/v1/Shipments/{id}/proof-of-delivery` (multipart) · `GET /api/v1/Shipments/{id}/proof-of-delivery` |
| Public tracking | `GET /api/v1/PublicTracking/{trackingNumber}` (no login, rate limited) |
| Files | `GET /api/v1/Files/{id}` (signature / photo download, bearer token required) |
| Master data | `ItemCategories`, `Locations`, `Parties`, `EventTypes`, `ReasonCodes`, `Carriers`, `Vehicles`, `Containers`, each with `GET` (search, filters, paging) · `POST` · `GET/PUT /{id}` · `GET /by-code/{code}` · `DELETE /{id}` (deactivate) · `POST /{id}/activate` |
| Legacy | `/api/v1/Products` CRUD (sample from before Thing-Tag) |

**Conventions**
- **Enums** are strings, for example `"InTransit"` or `"Sea"`.
- **Timestamps** are UTC. Requests accept any ISO timestamp with an offset.
- **Errors** are always **ProblemDetails** (RFC 9457) with a stable **`code`**, so the UI can show them in Thai or English:
  ```json
  { "status": 400, "title": "Bad Request",
    "detail": "Item 2: Category FOOD requires attributes: expiry",
    "code": "ITEM_MISSING_REQUIRED_ATTRIBUTES",
    "args": { "category": "FOOD", "attributes": ["expiry"] },
    "scope": { "kind": "item", "number": 2 } }
  ```
  - `detail` is the English message.
  - To translate, look up `code` in `GET /api/v1/ErrorCodes` and fill the `th` template with `args`, for example `ประเภท {category} ต้องกรอกข้อมูลเพิ่มเติม: {attributes}`.
  - `scope` tells you which entry of a batch request failed.
  - Framework errors carry codes too: `VALIDATION_FAILED` (field messages in `errors`), `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `RATE_LIMITED` (429, with `Retry-After`) and `INTERNAL_ERROR`.
- **Idempotency keys (safe retries)**
  - Send `Idempotency-Key: <unique value>` (1-255 visible ASCII characters, for example a UUID) on a POST that records events or creates records. Scanner and mobile apps should generate one key per action and reuse it for every retry of that action.
  - **Supported on:** `TrackedItems` create and `bulk`; `TrackingEvents` record, `scan`, `void` and `correct`; `Containers` `load`, `unload` and `scan`; `Shipments` create, `items`, `depart`, `arrive`, `customs`, `events`, `deliver`, `proof-of-delivery` and `cancel`.
  - **How it behaves:**
    - A retry with the same key and the same request returns the stored status, body and `Location`, with the header `Idempotent-Replayed: true`. Nothing runs again.
    - The same key with a different request (another endpoint or body) returns `422 IDEMPOTENCY_KEY_REUSED`.
    - Only successful responses are stored. After an error, fix the request and retry with the same key.
    - Two requests with the same key at the same moment run once; the second waits and gets the replay.
    - Keys are per user and kept for 24 hours (`Idempotency:RetentionHours`). Without the header, requests work as before.
  - A retry must resend the **same** data. Generate timestamps such as `occurredAt` once per action, not per attempt.
- **Paging**
  - Master data, items and shipments use page numbers (`page`, `pageSize`).
  - Tracking events use **cursor paging**: pass `nextCursor` back as `?cursor=`, and read `totalCount` only when you ask for it with `includeTotalCount=true`.

## Proof of delivery

When the courier hands over a shipment, the receiver signs on the courier's device, for example with a signature pad on a canvas. The app sends **one `multipart/form-data` request**:

| Field | Required | Notes |
|---|---|---|
| `signature` | ✔ | Image: PNG (from a canvas), JPEG or WebP, up to 1 MB |
| `photos` | | Up to 5 images, 5 MB each (the parcel at the door) |
| `receiverName` | ✔ | Who signed |
| `receiverRelation` | ✔ | `Recipient`, `FamilyMember`, `Colleague`, `Reception`, `Security`, `Neighbor` or `Other` |
| `signedAt`, `latitude`, `longitude`, `locationAccuracyMeters`, `deviceInfo`, `note` | | `signedAt` defaults to now |
| `refusedItems[i].tagCode` / `.reasonCode` / `.note` | | Items the receiver refused, with a `DELIVERY_FAILED` reason such as `REFUSED` |

In one transaction, the API:
- Checks the files by their **content**, not the file name or declared type, so a renamed text file is rejected.
- Stores the files with their **SHA-256**.
- Saves the proof, marks the shipment **Delivered**, and records `DELIVERED` for received items and `DELIVERY_FAILED` for refused ones.
- Deletes the uploaded files if anything fails, so nothing is left half-saved.

**Rules**
- **Signature required by default.** Shipments have `requiresSignature = true`, so plain `/deliver` returns `409 SIGNATURE_REQUIRED`. Set `requiresSignature: false` on a shipment to allow delivery without a signature.
- **One proof per shipment.** A second one returns `409 PROOF_OF_DELIVERY_EXISTS`.

**Files**
- **Storage.** Files are stored under `FileStorage:RootPath` (`App_Data/files` by default; Docker Compose uses the `files-data` volume). They're served by `GET /api/v1/Files/{id}` with the bearer token, so a frontend fetches the image as a blob rather than using `<img src>`.
- **Personal data.** Signatures and photos are personal data under PDPA; role-based access to them comes with the permissions phase.

## Public tracking

`GET /api/v1/PublicTracking/{trackingNumber}` is for customers and receivers, for example a "Track your shipment" page. It needs no token, and the tracking number is case-insensitive.

**What it returns**
- The shipment's status, customs status, origin and destination, planned pickup, ETA (the last leg's planned arrival), delivery time, whether delivery was signed for, and the number of pieces.
- The legs, with mode, carrier name, route and ETD/ETA vs ATD/ATA.
- A **timeline, newest first**: the shipment's own events (departures, arrivals, customs, delivery) plus other events of its items while they were in the shipment, such as hub scans. An event recorded for several pieces at once appears once, with a `pieces` count. Voided events are left out.
- Every location has its IANA `timeZone`, so the page can show local times.

**What it never returns**
- Sender and receiver, the customer's reference and notes.
- Who recorded an event, event notes and GPS.
- Vehicles and transport documents.
- The receiver's name, signature and photos.
- The name of a customer-address location. Only its type, province and country are shown.

**Abuse protection**
- Requests are limited per client IP: 30 per 60 seconds by default, set with `RateLimiting:PublicTracking:PermitLimit` / `WindowSeconds`. Past that, the API returns `429 RATE_LIMITED` with `Retry-After`.
- An unknown tracking number gets a plain `404 NOT_FOUND`.
- Behind a reverse proxy, configure forwarded headers so the limit applies to the real client IP rather than the proxy's.

## Authentication and authorization

- **Login** (`POST /api/v1/Auth/login`) returns a JWT with a `role` claim, valid for 2 hours. Passwords are hashed with BCrypt.
- **Secure by default.** A global fallback policy requires an authenticated user on every endpoint except login, the error catalog and public tracking.
- **Permissions are data, not code.** Actions use `[Authorize(Policy = "...")]`, and `Authorization/PermissionAuthorizationHandler.cs` checks the caller's role against the `RolePermissions` table on each request. Granting or revoking access means changing a row, not redeploying.
- **Seeded accounts and roles.** `Scripts/003_users_seed_admin.sql` creates a bootstrap `admin` account; **change its password immediately**. `Scripts/005_permissions_seed.sql` grants the `Admin` role everything.

> **Current phase:** Thing-Tag endpoints don't have per-endpoint permissions yet, so any logged-in user can call them. Permissions will be added for all features together once the feature set is complete.

## Technology stack

- **ASP.NET Core 10** / C# with controllers, the built-in DI container, `IExceptionHandler`, ProblemDetails, **Asp.Versioning** and a hosted background service
- **Docker Compose** for the local stack (PostgreSQL 18 + schema scripts + API)
- **Entity Framework Core 9** with **Npgsql**, using `jsonb` for item attributes and `text[]` for list columns
- **PostgreSQL 14+** with range partitioning and `pg_trgm`. Development uses PostgreSQL 18 in Docker.
- **JWT Bearer** authentication and **BCrypt** password hashing
- **OpenAPI / Swagger** (Swashbuckle)
- **Bruno** for API testing

> EF Core and Npgsql are on 9.x while the app targets .NET 10. Align the versions before a production release.

## Getting started

### Quick start with Docker Compose

This needs only Docker. It starts PostgreSQL, applies all SQL scripts, and runs the API:

```bash
cp .env.example .env          # optional: change passwords / JWT key / ports
docker compose up --build     # API: http://localhost:5106/swagger
```

- **Ports.** The database is exposed on host port **5433**, so it doesn't clash with a local PostgreSQL. You can change it in `.env`.
- **Schema updates.** `db-migrate` re-applies the idempotent scripts on every start, so new scripts are picked up automatically.
- **Uploaded files.** Signatures and photos go to the `files-data` volume. The API container runs as the non-root `app` user.
- **Stopping.** `docker compose down` keeps the data. Add `-v` to delete the database.

### Manual setup

#### 1. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL 14+, for example with Docker:

```bash
docker run --name postgres-server -e POSTGRES_PASSWORD=<password> -p 5432:5432 -d postgres:18
docker exec postgres-server createdb -U postgres myfirstapi_db
```

#### 2. Configure

`appsettings.json` holds the connection string and JWT settings. For anything beyond local development, override them with `dotnet user-secrets` or environment variables, and **don't commit real secrets**:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=myfirstapi_db;Username=postgres;Password=<password>"
export Jwt__Key="<long-random-secret>"
```

#### 3. Create the schema

The database is built from numbered, **idempotent** SQL scripts in `Scripts/` (`001` to `030`). Re-running them only applies what's new.

With `psql` installed on the host:

```bash
psql -h localhost -p 5432 -U postgres -d myfirstapi_db -f Scripts/run_all.sql
```

With PostgreSQL in Docker (the `\ir` includes in `run_all.sql` don't work over stdin, so run the files in order):

```bash
for f in Scripts/0*.sql; do
  docker exec -i postgres-server psql -U postgres -d myfirstapi_db -v ON_ERROR_STOP=1 < "$f" || break
done
```

| Scripts | Contents |
|---|---|
| `001`–`005` | Products, users, admin account, permissions |
| `006`–`012` | Tracked items; item categories, locations, parties, event types (+ seed) |
| `013`–`021` | Multimodal: location codes/time zones, dangerous goods, carriers, vehicles, containers, customs fields, shipments/legs, tracking events, multimodal event types |
| `022`–`024` | Reason codes (+ seed), void/correction columns |
| `025`–`027` | Performance: monthly partitioning of `TrackingEvents`, trigram search indexes, foreign-key indexes |
| `028` | `English \| ภาษาไทย` comments on every table and column |
| `029` | Proof of delivery: stored files, proofs, photos, `Shipments.RequiresSignature` |
| `030` | Idempotency keys (stored responses for safe retries) |

#### 4. Run

```bash
dotnet run                          # http://localhost:5106
dotnet run --launch-profile https   # https://localhost:7195
```

Open `http://localhost:5106/swagger`, log in with `POST /api/v1/Auth/login`, and click **Authorize**.

#### CORS (frontend origins)

Browsers only let a frontend call the API from origins listed in `Cors:AllowedOrigins`.
- **Development.** `appsettings.Development.json` allows the common dev servers: `http://localhost:3000` (React / Next.js), `:5173` (Vite) and `:4200` (Angular).
- **Other environments.** Set the origins through configuration, for example `Cors__AllowedOrigins__0=https://app.example.com`.

## Example

```bash
# Log in
TOKEN=$(curl -s -X POST http://localhost:5106/api/v1/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"<password>"}' | jq -r .token)

# Register an item
curl -X POST http://localhost:5106/api/v1/TrackedItems \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"tagCode":"QR-0001","name":"Notebook","currentLocationId":1}'

# Scan several tags at a hub
curl -X POST http://localhost:5106/api/v1/TrackingEvents/scan \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"tagCodes":["QR-0001","QR-0002"],"eventTypeCode":"ARRIVED_AT_HUB","locationId":1}'

# Item timeline, oldest first
curl "http://localhost:5106/api/v1/TrackingEvents?tagCode=QR-0001&oldestFirst=true" \
  -H "Authorization: Bearer $TOKEN"
```

The Bruno collection has complete, working examples of every endpoint, including a full journey from Bangkok to Laem Chabang by truck, on to Tokyo by vessel and then to the customer.

## Testing

API tests live in the **[Bruno](https://www.usebruno.com/)** collection in `bruno/` (228 requests, including proof-of-delivery uploads that use the images in `bruno/fixtures/`). They also check error `code`s. The collection passes against both `dotnet run` and `docker compose`:

1. In Bruno, choose **Open Collection** → `bruno/`, then select the **Local** environment.
2. Run **01 Auth / Login**. It stores the JWT for all other requests.
3. Or run the whole collection with the **Runner**.
   - Requests are chained through variables.
   - Codes include a per-run id, so the collection can be re-run against the same database.

There is no automated unit or integration test project yet.

## Performance and maintenance

`TrackingEvents` grows the fastest: one row per item per scan or movement. The design keeps everyday screens fast as it grows:

- **Current state lives on the item**, so the busiest screens never read history.
- **Monthly partitions** keep indexes small and let date-range queries skip whole months. Old months can later be archived in one step.
- **Partitions are created ahead.** `PartitionMaintenanceService` creates them 3 months ahead at startup and daily.
- **Expired idempotency keys are deleted hourly** by `IdempotencyKeyCleanupService`, so `IdempotencyKeys` only holds the last 24 hours.
- **Cursor paging** keeps deep pages as fast as the first.
- **`pg_trgm` indexes** serve substring searches, and **every foreign key is indexed**.

Measured with 3M events, 200k items and 50k shipments (`Scripts/dev/`):

| Case | Before | After |
|---|---|---|
| Latest events, first page | 236 ms | 4.7 ms |
| Item substring search | 150 ms | 3.8 ms |
| Shipment search by B/L | 142 ms | 24 ms |
| Deep page (row 50,000) | 65 ms | 5 ms |
| Scan 100 tags (write) | 25 ms | 25 ms |

`Scripts/maintenance/README.md` has the preventive-maintenance schedule (daily, weekly, monthly and quarterly) and performance targets. The tools:

```bash
docker exec -i postgres-server psql -U postgres -d myfirstapi_db < Scripts/maintenance/health_check.sql
```

The health check reports table growth, partitions, bloat, unused indexes, slow queries, unindexed foreign keys and undocumented columns.

## Security considerations

Before deploying beyond local development:

1. **Move secrets out of `appsettings.json`.** The DB password and JWT key there are development values and are public in this repository's history, so **generate a new JWT key**.
2. **Change the seeded `admin` password.**
3. **Restrict access to signatures and delivery photos.** They are personal data (PDPA). Use cloud storage with encryption and backups for `FileStorage` in production.
4. **Apply per-endpoint permissions** to the Thing-Tag endpoints (planned) and review the `RolePermissions` table.
5. **Set `Cors:AllowedOrigins`** to the real frontend origin only, use HTTPS everywhere, and add monitoring. Public tracking is rate limited per IP; behind a proxy, set up forwarded headers so the limit sees real client IPs.

## Roadmap

- [x] Database-backed authentication, BCrypt hashing, permission-based authorization
- [x] DTOs, validation and centralized error handling
- [x] Tagged items, master data, tracking events with void/correction and reason codes
- [x] Containers / consolidation and multimodal shipments with customs
- [x] Partitioning, cursor paging, search indexes and maintenance tooling
- [x] API versioning (`/api/v1`), coded bilingual error catalog, configurable CORS, Docker Compose stack
- [x] Proof of delivery: receiver signature, photos, GPS, refused items
- [x] Public tracking API (no login, by tracking number, rate limited)
- [ ] Document attachments (B/L, invoices) on the same file storage
- [ ] Dashboards / reports and notifications (email, webhook)
- [ ] Audit log
- [ ] Per-endpoint permissions for Thing-Tag features, user management, refresh tokens
- [x] Idempotency keys for safe retries of scans and operations
- [ ] Concurrency control and offline scanning (batch upload of queued scans)
- [ ] Automated tests, CI/CD, health checks

## Contributing

```bash
git checkout -b feature/<short-description>
# make changes, update bruno/ tests, README.md and CLAUDE.md
git commit -m "feat : describe the change"
git push origin feature/<short-description>
```

Then open a pull request with a summary, the testing you did, and any database scripts to apply. Commit messages use a Conventional Commits-style `type : message`, where the type is `feat`, `fix`, `perf`, `docs`, `refactor` or `chore`.

## License

No license is currently specified. If this project is intended for public reuse, add an explicit open-source license (for example MIT or Apache-2.0).

## Author

**mcfr2008** · https://github.com/mcfr2008
