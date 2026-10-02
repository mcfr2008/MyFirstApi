# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Goal

**Thing-Tag** is an end-to-end tracking system that follows the movement of any item from origin to destination, giving the kind of visibility a professional logistics network provides.

> Thing-Tag คือระบบติดตามอัจฉริยะที่ออกแบบมาเพื่อเกาะติดทุกการเคลื่อนไหวของสิ่งของทุกประเภท ตั้งแต่ต้นทางจนถึงปลายทางอย่างไร้รอยต่อ มอบประสบการณ์การขนส่งที่โปร่งใสและตรวจสอบได้จริง เหมือนกับระบบโลจิสติกส์มาตรฐานระดับสากล

Built so far: auth and permissions; master data; tagged items; append-only tracking events; containers/consolidation; multimodal shipments (road, rail, air, sea and courier legs, with customs); proof of delivery; public tracking; idempotency keys for safe retries; service areas and lanes; carbon footprint; route planning; misroute detection; and return to sender (manual and automatic). The legacy `Products` CRUD predates Thing-Tag. For now this project is purely the backend REST API for a separate frontend app, so it serves JSON only (no server-rendered UI). New features should build toward that goal and reuse the existing layering and permission model.

## Commands

```bash
dotnet restore
dotnet build
dotnet run                      # http://localhost:5106 (profile "http")
dotnet run --launch-profile https   # https://localhost:7195
docker compose up --build           # full stack: PostgreSQL (host port 5433) + scripts + API on :5106
```

- Swagger UI (`/swagger`) and OpenAPI (`/openapi/v1.json`) are only mapped in the Development environment. Swagger has a Bearer "Authorize" button; get a token from `POST /api/v1/Auth/login`.
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
- **Errors.** Services throw `ApiException`s created by the factory methods in `Exceptions/Errors.cs`, e.g. `throw Errors.TagCodeExists(codes)`.
  - `Errors.cs` is the error catalog. Each `ErrorDefinition` has a stable code, an HTTP status and `{placeholder}` templates in **English and Thai**. Add new errors there; never throw ad-hoc messages.
  - `ApiExceptionHandler` writes ProblemDetails with `detail` (English), `code`, `args` and an optional `scope`. `.In(ErrorScope.Item(n) / Leg(n))` tags which entry of a batch request failed.
  - `Program.cs` `CustomizeProblemDetails` adds codes to framework errors (`VALIDATION_FAILED`, `UNAUTHORIZED`, `NOT_FOUND`, ...), and `UseStatusCodePages` gives empty 401/404 responses a body.
  - `GET /api/v1/ErrorCodes` (anonymous) serves the catalog so the frontend can translate.
  - `ReferenceResolver` "field" arguments are request property names (`categoryId`), so the UI can highlight the right input.
  - Controllers have no try/catch.
- **Versioning.** Routes are versioned with Asp.Versioning (URL segment). New controllers use `[Route("api/v{version:apiVersion}/[controller]")]`, and controllers without `[ApiVersion]` default to 1.0. Breaking changes go into a new version rather than changing v1.
- **CORS.** Policy "Frontend" allows the origins in `Cors:AllowedOrigins` (dev servers are listed in `appsettings.Development.json`, and the list is empty in `appsettings.json`).
- Enums are serialized as strings (`[JsonConverter(typeof(JsonStringEnumConverter<T>))]` on the enum) and stored as strings (`HasConversion<string>()`).
- Lists are paged with `PagedResult<T>`. Items are archived (`IsArchived`) instead of deleted so tracking history survives.
- **Master data** (`ItemCategories`, `Locations`, `Parties`, `EventTypes`, `ReasonCodes`, `Carriers`, `Vehicles`, `Containers`, `ServiceAreas`, `Lanes`, `EmissionFactors`) is built on shared generic bases: `Services/MasterDataService<...>` and `Controllers/MasterDataController<...>`.
  - Each concrete service only defines its DbSet, projection, search, filters, order and `Apply`. Each concrete controller only adds `[Route]`.
  - Entities implement `Models/IMasterData`, and responses implement `IMasterDataResponse`.
  - Codes are unique and stored upper-case. `DELETE` deactivates (`IsActive=false`) instead of deleting.
  - An inactive row stays valid on records that already reference it, but can't be newly assigned (`Services/ReferenceResolver`).
  - Async cross-checks go in the `ValidateAsync` hook, which runs before `Apply` so the entity still holds its old values.
  - To add a new master table: add an entity, DTOs, a service, an interface, a controller, a SQL script, a DbSet and a DI registration.
- `TrackedItem` optionally references `ItemCategory`, `Location` (`CurrentLocationId`), `Party` (owner) and `Container` (`CurrentContainerId`), all with FK `ON DELETE RESTRICT`. A category's `RequiredAttributes` keys must be present in the item's `Attributes`.
- Timestamps are UTC `DateTime` stored as `timestamptz`. Request timestamps are `DateTimeOffset`, converted with `.UtcDateTime`. The current username comes from `ICurrentUser` (the JWT `sub` claim, which JwtBearer maps to `ClaimTypes.NameIdentifier`).

### Service areas and lanes (route planning, phase 1)

- **What a row is.** A `ServiceArea` maps one postal code, or a whole province (`PostalCode` null), to:
  - a `StationLocationId`: the Branch, DropPoint, Hub or Warehouse that does pickup and delivery in the area;
  - a `HubLocationId`: the sorting hub that station feeds, which must be of type Hub.
  - A location of the wrong type gives `LOCATION_TYPE_NOT_ALLOWED`.
- **No overlaps.** There is one area per (Country, PostalCode) and one province-wide area per (Country, lower(Province)). Inactive rows count too.
  - Enforced by `ServiceAreaService.ValidateAsync` (`SERVICE_AREA_OVERLAP`) and by partial unique indexes in `Scripts/031`.
- **Resolve.** `GET /api/v1/ServiceAreas/resolve` takes `postalCode`/`province`/`country`, or a `locationId` (which uses that location's address fields).
  - It matches the active postal-code area first, then the active province-wide area (`matchedBy`), or returns `404 SERVICE_AREA_NOT_COVERED`.
  - Province matching is case-insensitive equality, so the area's province must be spelled the same way as `Locations.Province` (the seed data uses Thai names).
  - Thai postal codes must be 5 digits.
- **Lanes** (`Lane`, `Scripts/032`) are one-way scheduled connections between network points. The return trip is its own lane.
  - An end can be any location type except `CustomerAddress`, because the last mile to a customer is a shipment leg.
  - The carrier must run the lane's mode. `TransportModes.SameFamily` (moved from `ShipmentService` to `Models/TransportMode.cs`) treats Road and Courier as one.
  - `TransitTimeMinutes` is required (1 minute to 60 days).
  - `DepartureTimes` is a `text[]` of `"HH:mm"` in the origin location's time zone, de-duplicated and sorted. Empty means on demand.
  - `OperatingDays` is a `text[]` of `Weekday` names, sorted Monday-first. Empty means every day. The response exposes it as typed `Weekday` values via `OperatingDayNames`, like `Carrier.Modes`.
  - The list filters are `originLocationId`, `destinationLocationId`, `locationId` (either end), `mode` and `carrierId`.
- **Route planner.** `POST /api/v1/Routes/plan` is implemented in `RoutesController` and `Services/RoutePlannerService.cs`. It is read-only and saves nothing.
  - **Ends.** A non-`CustomerAddress` location is used as is. An address or a customer location goes through `IServiceAreaService.FindAsync` to its station. A missing area gives `ROUTE_ENDPOINT_NOT_COVERED`.
  - **Search.** It loads all active lanes once and runs Dijkstra (up to 12 legs) from the origin station to the destination station.
    - `Fastest` is time-dependent. `NetworkLane.NextDeparture` takes the next `DepartureTimes`/`OperatingDays` slot in the lane origin's IANA time zone. Empty times mean the lane leaves on arrival, and days not in `OperatingDays` are skipped.
    - `Shortest` and `LowestEmissions` use static weights: km, and kg CO2e per tonne.
    - Lanes with an unknown distance are skipped for those two objectives.
  - **Alternatives.** Simplified Yen's: the search is re-run excluding each lane of the best path, keeping distinct paths ranked by the objective.
  - **Reuse.** Distance and factor helpers are shared with the carbon footprint in `Services/TransportEstimates.cs`.
  - **`laneIds`.** A `RoutePlanRequest` with `laneIds` skips the search and evaluates that path (`ChosenPath`): every lane must be active (`ROUTE_LANE_NOT_AVAILABLE`) and chained station to station (`ROUTE_LANES_NOT_CONNECTED`).
- **Auto-route a shipment.** `POST /api/v1/Shipments/{id}/route` is `[Idempotent]` and implemented in `ShipmentService.Routing.cs`, a partial class. `ShipmentService` now depends on `IRoutePlannerService`.
  - It runs on Planned shipments only, with action `route`.
  - It plans from the shipment's origin to its destination location.
  - **First mile.** When the origin isn't its own station, it adds a Courier first-mile leg (`FirstMileMinutes`, default 120). The planner starts at `readyAt + firstMile`, where `readyAt` defaults to `PlannedPickupAt`, else now.
  - **Lanes.** It adds one leg per lane, with the lane's mode, carrier and the scheduled times as ETD/ETA.
  - **Last mile.** It adds a Courier last-mile leg (`LastMileMinutes`, default 240).
  - **Saving.** Legs replace the existing ones through `ApplyLegsAsync`: in place by sequence, with the same chain and transport rules.
  - It returns `ShipmentRouteResponse { shipment, plan }`.
  - The lane a leg came from isn't stored, because legs keep only origin/destination/mode/carrier.
- **Misroute detection** (`Scripts/034`).
  - **Flagging.** `TrackingEventRecorder.FindOffRouteShipmentsAsync` sets `TrackingEvent.OffRouteShipmentId` at record time when all of these hold:
    - the event has a location and no `ShipmentId` (events from shipment operations aren't checked);
    - the item is in an open (Planned/InTransit) shipment that has legs;
    - the location isn't the shipment's origin, its destination, or any leg's origin or destination.
  - The column is written once and never updated, like the rest of the event.
  - **Responses.**
    - Single, scan and correct responses carry `offRouteShipment`.
    - `EventsRecordedResponse.OffRouteEvents` counts flagged events, for container scans.
    - `GET /TrackingEvents?offRoute=` filters on the flag.
  - **"Off route now"** lives in `ShipmentService.RouteCheck.cs`: an item's current location equals a non-voided flagged scan location that is still not on the route.
    - So scanning back on route, re-routing, or voiding the scan clears it.
    - Items that were elsewhere before joining the shipment have no flag and don't count.
    - Used by `GET /Shipments/{id}/route-check` and `GET /Shipments?offRoute=true`. The latter is `MisroutedShipmentIds()`, which starts from the partial index on `OffRouteShipmentId`.
  - **Not alerted yet.** Off-route scans aren't pushed anywhere. Notifications would be a separate feature.

### Tracking model (the core of Thing-Tag)

- **State only changes through events.** An item's `Status` and `CurrentLocationId` are never edited directly: `PUT` doesn't touch them, and there is no status endpoint. Every change is a `TrackingEvent`, a row that is never updated or deleted.
  - `EventType.ResultingStatus` decides the new status (null means the event is informational).
  - An event only updates the item if its `OccurredAt >= item.LastEventAt`. A back-dated event is kept in the history without rewinding the item.
- **Journeys end at terminal events.** `EventType.IsTerminal` (`DELIVERED`, `RETURNED`) is enforced in `TrackingEventRecorder.EnsureJourneyOpenAsync`.
  - An event with no `ShipmentId` (manual, scan, container) whose `OccurredAt >= item.LastEventAt` is rejected with `ITEM_JOURNEY_ENDED` when the item's latest non-voided event is terminal. Events being voided in the same unit of work don't count.
  - It is allowed when the item joined an open shipment at or after that event. The check reads the database plus `ShipmentItems` staged in the change tracker.
  - Back-dated events and shipment-operation events aren't checked.
- **Mistakes are voided, not deleted.**
  - `POST /api/v1/TrackingEvents/{id}/void` flags the event (`IsVoided`, with who/when/why). `/correct` voids it and records a replacement linked by `ReplacesEventId`.
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
  - The lifecycle is `Planned → InTransit → Delivered`, `Planned → Cancelled`, or `InTransit → ReturnedToSender` (see Return to sender):
    - depart/arrive endpoints set ATD/ATA and record mode-specific events for every item;
    - a customs `Hold` blocks departure and delivery;
    - international shipments (origin and destination in different countries) start with customs `Pending`.
  - An item can be in only one open (Planned or InTransit) shipment at a time. A full `PUT` is only allowed while the shipment is Planned; after that, only a leg that hasn't departed can be edited.
- `Dictionary` properties map to `jsonb`, which relies on `EnableDynamicJson()` in the `UseNpgsql` setup in `Program.cs`.

The dev PostgreSQL database runs in a Docker container named `postgres-server`, and `psql` isn't installed on the host. Apply a script with `docker exec -i postgres-server psql -U postgres -d myfirstapi_db -v ON_ERROR_STOP=1 < Scripts/NNN_x.sql`. The seeded login is `admin` / `ChangeMe123!` (see `Scripts/003_users_seed_admin.sql`).

### Proof of delivery and file storage

- **Endpoint.** `POST /api/v1/Shipments/{id}/proof-of-delivery` (multipart, `ShipmentService.ProofOfDelivery.cs`, a partial class of `ShipmentService`) does everything in one `SaveChanges`:
  - validates image files by magic bytes (`Services/FileSignatures.cs`)
  - stores them through `IFileStorage`
  - saves `ProofOfDelivery`, `StoredFile` and photo rows
  - delivers the shipment and records `DELIVERED` / `DELIVERY_FAILED` (refused items) events
- **Cleanup.** If anything fails after files were written, they are deleted in the `catch`.
- **Signature rule.** `Shipment.RequiresSignature` (default true) makes plain `/deliver` return `SIGNATURE_REQUIRED`. `EnsureDeliverable` holds the shared delivery checks.
- **Storage.** `IFileStorage` is implemented by `LocalFileStorage` (disk under `FileStorage:RootPath`, default `App_Data/files`, which is git-ignored and a Docker volume). A cloud implementation can replace it.
  - `StoredFile.Id` is a random GUID used in `GET /api/v1/Files/{id}`, and `Sha256` is kept as tamper evidence and the ETag.
- **JSON keys.** `ProofOfDelivery.RefusedItems` is jsonb with camelCase keys (`[JsonPropertyName]`), so SQL like `->>'reasonCode'` matches the API.

### Return to sender

- **Endpoint.** `POST /api/v1/Shipments/{id}/return-to-sender` is `[Idempotent]` and lives in `ShipmentService.Return.cs`. It does everything in one `SaveChanges`.
- **Checks, in order.**
  1. An existing return (`RETURN_ALREADY_CREATED`) is checked first, so a returned shipment names its return.
  2. The status must be InTransit or Delivered.
  3. Items are the shipment's items with `Status != Delivered`; none gives `NOTHING_TO_RETURN`. A Delivered shipment only returns its refused or undelivered items.
- **Plan before writing.** With `AutoRoute`, `PlanLegsAsync` (shared with auto-route) plans from `locationId` (default: the destination) back to the original origin *before* anything is written, so a missing route changes nothing.
- **Writes.**
  - `RETURNED` with the reason, system-managed, carrying the original `ShipmentId`, which also skips the misroute check.
  - InTransit → `ReturnedToSender`. A Delivered shipment keeps its status.
  - A new Planned shipment: parties swapped, `ReturnOfShipmentId` set, customs `Pending` when the countries differ, `RequiresSignature` true.
  - `ShipmentItems` rows are added directly. The open-shipment check reads the database, where the original is still InTransit, so it would wrongly reject them.
  - Legs go through `ApplyLegsAsync`.
- **Status semantics.** `ReturnedToSender` is closed. Open means only Planned/InTransit everywhere, and `RecordEventAsync` rejects it (`recordEvent`). Public tracking ends a returned original's item-event window at `UpdatedAt`.
- **Schema.** `Shipments.ReturnOfShipmentId` has a partial unique index, so there is one return per shipment (`Scripts/035`).
- **Responses.**
  - `ShipmentResponse` adds `ReturnOf`, `ReturnShipment` and `FailedDeliveryAttempts`.
  - The latter two are filled by `EnrichAsync` for both detail and list. Failed attempts are distinct `OccurredAt` of non-voided `DELIVERY_FAILED` events with the shipment's id.
  - The list filter is `isReturn`.
- **Automatic return.** `ReturnCoreAsync` stages a return without saving; the manual endpoint and `AutoReturnIfLimitReachedAsync` both use it.
  - **Trigger.** `RecordEventAsync`, which now loads the shipment with details, calls the auto-return after staging a `DELIVERY_FAILED`, all in the same `SaveChanges`.
  - **Conditions.** The shipment must be InTransit and not itself a return. The limit is `Shipment.MaxDeliveryAttempts ?? Returns:MaxDeliveryAttempts` (default 3), where `0` means never. Attempts are the distinct saved times plus the new one.
  - **The return it creates.** It uses reason `MAX_ATTEMPTS_REACHED` (seeded in `Scripts/036`, allowed only for `RETURNED`) at the attempt time, from the destination.
  - **Missing route.** With `routeIsOptional`, a missing route (`ROUTE_*` codes) creates the return without legs instead of failing the attempt.
  - **Response.** `EventsRecordedResponse.ReturnTrackingNumber` reports the new return.
  - `ShipmentResponse.MaxDeliveryAttempts` is the effective limit (passed into `From`).
- **Not yet.** RMA isn't supported, and a return can't be undone, since `RETURNED` is system-managed.

### Carbon footprint

- **Calculation.** `Services/CarbonFootprintService.cs` (`ICarbonFootprintService`) computes per leg `CO2e kg = tonnes x km x gCO2e/tkm / 1000` (ISO 14083 / GLEC, well-to-wheel). It is served by `GET /api/v1/Shipments/{id}/emissions` and summarised in public tracking (`emissions`).
- **Inputs:**
  - **Mass:** the sum of `TrackedItem.WeightKg` over the current `ShipmentItems`.
  - **Distance:** an active `Lane` with `DistanceKm` between the same two points in the same mode family, preferring the exact mode. Otherwise the haversine great-circle distance adjusted by mode (Sea x1.15, Air +95 km, others x1.2). Otherwise `Unknown`.
  - **Factor:** the active `EmissionFactor` for (mode, leg carrier), else for (mode, no carrier).
- **Missing data is never guessed.** Missing weight, distance or factor goes into `DataGaps` and makes `IsComplete` false, and the totals only sum the legs that could be calculated.
- **Emission factors.** `EmissionFactors` is master data (`Scripts/033`).
  - There is one row per (Mode, CarrierId), with a NULL carrier as the mode's default. This is enforced by `EMISSION_FACTOR_OVERLAP` and by a unique index on `(Mode, COALESCE(CarrierId, 0))`.
  - The carrier must run the mode.
  - The seeded defaults are **indicative**, and docs must keep saying so. Never present them as official GLEC values.
- **Not stored yet.** Results are calculated on demand. A snapshot at delivery or a monthly report would be a later step.

### Public tracking

- **Endpoint.** `GET /api/v1/PublicTracking/{trackingNumber}` (`PublicTrackingController`, `Services/PublicTrackingService.cs`, `Dtos/PublicTrackingDtos.cs`) is anonymous and read-only.
- **Separate DTOs on purpose.** Never reuse `ShipmentResponse` or the tracking event DTOs here.
  - `PublicTracking*Response` leaves out parties, reference, notes, `RecordedBy`, GPS, vehicles, documents and POD details.
  - `PublicLocationResponse` hides the name of `CustomerAddress` locations.
  - Anything added to these DTOs is visible to anyone with a tracking number.
- **Timeline.** It combines:
  - the non-voided events with this `ShipmentId`;
  - the non-voided events of its items with no `ShipmentId`, from each item's `ShipmentItem.AddedAt` until `DeliveredAt` (Delivered) or `UpdatedAt` (Cancelled).
  - Rows are grouped by (event type, `OccurredAt`, location, reason) into one entry with a `pieces` count, newest first.
- **Rate limiting.** The `PublicTracking` policy is a fixed window per client IP (`RateLimiting:PublicTracking:PermitLimit`/`WindowSeconds`, default 30/60s), set up with `AddRateLimiter` in `Program.cs`.
  - Rejections are 429 with `Retry-After`, and `UseStatusCodePages` + `CustomizeProblemDetails` give them the `RATE_LIMITED` code.
  - Use `[EnableRateLimiting]` on any future anonymous endpoint.

### Idempotency keys

- **Opt-in per action.** `[Idempotent]` (`Idempotency/IdempotentAttribute.cs`) turns on `Idempotency/IdempotencyFilter.cs` for a POST action. It is on every POST that records events or creates records in the item, event, container and shipment flows, but not on master data (unique codes already block duplicates) or on PUT/DELETE (idempotent by nature).
  - Put `[Idempotent]` on any new POST of that kind.
  - A decorated action must return an `ObjectResult` or `StatusCodeResult`. Other result types throw, because they can't be stored.
- **Same transaction.** When a request has an `Idempotency-Key`, the filter opens a transaction on the scoped `AppDbContext` and inserts the key row with `ON CONFLICT DO NOTHING`. The action then runs, and its service `SaveChanges` joins that transaction. A 2xx response is stored and committed together with the business changes.
  - Exceptions and non-2xx responses roll back, and the key isn't kept.
  - Services must not start their own transactions (`BeginTransaction`) or use an execution strategy with retries. Both would conflict with the filter's transaction.
- **Request hash.** It is SHA-256 of the method, the path and query, and the **bound** action arguments. Uploaded `IFormFile`s count by name, size and content hash.
  - A different hash for the same key gives `422 IDEMPOTENCY_KEY_REUSED`, and a bad key gives `400 IDEMPOTENCY_KEY_INVALID`.
  - Replays add `Idempotent-Replayed: true`, which is exposed through CORS.
- **Concurrency.** A concurrent request with the same key blocks on the uncommitted row, gets 0 rows inserted after the first request commits, and replays its response.
- **Storage.** Keys are per user (PK `Username`, `Key`) and live in `IdempotencyKeys` (`Scripts/030`).
  - They expire after `Idempotency:RetentionHours` (default 24). `IdempotencyKeyCleanupService` deletes expired rows hourly.
  - An expired key found on use is deleted and reused.

### Performance and data growth

`TrackingEvents` is the fast-growing table. `Scripts/maintenance/README.md` has the preventive-maintenance plan and the before/after benchmarks.
- **Partitioning.** It is range-partitioned by month on `OccurredAt` (`Scripts/025`).
  - The PK is `("Id", "OccurredAt")` and there's no DB foreign key for `ReplacesEventId`, but EF still models `Id` as the key.
  - A lookup by `Id` alone probes every partition. When the time is known, also filter on `OccurredAt` (see `TrackingEventService.GetByIdsAsync`).
  - `PartitionMaintenanceService` (a hosted service) creates partitions 3 months ahead at startup and daily.
- **Cursor paging.** `GET /api/v1/TrackingEvents` returns `CursorPagedResult` (`nextCursor`/`hasMore`) instead of page numbers.
  - The cursor is a base64url-encoded `"<OccurredAt ticks>_<Id>"`.
  - `totalCount` is only computed with `includeTotalCount=true`.
  - Use this pattern for any other list that can grow to millions of rows.
- **Search indexes.** Substring search (`ILIKE '%x%'`) relies on `pg_trgm` GIN indexes (`Scripts/026`).
  - Every column in a searched OR needs one.
  - Don't OR across a join or `EXISTS`: build matching ids with `UNION` instead (see `ShipmentService.GetShipmentsAsync`).
- **Foreign-key indexes.** Every foreign-key column needs an index. PostgreSQL doesn't create them, and a missing one turns deletes of the referenced row into full scans (`Scripts/027`, health check section 6).
- **Master-data cache.** Event types and reason codes are read through `IMasterDataCache` (IMemoryCache, 5-minute TTL).
  - `EventTypeService` and `ReasonCodeService` invalidate it through the `MasterDataService.OnChanged` hook.
  - Cached entities are detached, so assign their ids and never attach them or use them as navigation properties.
- **Dev scripts.** `Scripts/dev/` (load-test seed/cleanup, `benchmark.py`) and `Scripts/maintenance/` (health check, partitions, `pg_stat_statements`) are **not** in `run_all.sql`.

### Schema changes

`Scripts/` holds numbered, idempotent SQL files (`CREATE TABLE IF NOT EXISTS`, `ON CONFLICT DO NOTHING`), one per feature, schema before seed. When adding a table/column:
1. Add a new `NNN_*.sql` file (next number) and add an `\ir` line for it in `Scripts/run_all.sql`.
2. Update the entity in `Models/` and `DbSet`/`OnModelCreating` in `AppDbContext` to match. The two must be kept in sync manually.
3. **Scripts must survive being re-run in full.** `db-migrate` in docker-compose runs `run_all.sql` on every start. Guard anything that depends on objects a later script removes; see the `Category` index in `006`. Test by running every script twice on an empty database.
4. Add a `COMMENT ON TABLE` / `COMMENT ON COLUMN` for every new table and column, written as `'English | ภาษาไทย'`. For enum columns, list the allowed values. Put it in the same script or in `Scripts/028_table_column_comments.sql`. `health_check.sql` section 7 lists anything left undocumented.

### Authorization (database-driven permissions)

This spans several files and is the main non-obvious design:

- **Secure by default**: `Program.cs` sets a fallback policy requiring an authenticated user. Endpoints must opt out with `[AllowAnonymous]`: only `AuthController.Login`, `ErrorCodesController` and `PublicTrackingController` do.
- Actions use `[Authorize(Policy = Permissions.X)]` with constants from `Authorization/Permissions.cs` (e.g. `"Products.Read"`).
- `Authorization/PermissionPolicyProvider.cs` (custom `IAuthorizationPolicyProvider`) turns *any* policy name into a `PermissionRequirement` — policies are never registered individually in `Program.cs`.
- `Authorization/PermissionAuthorizationHandler.cs` checks the JWT's role claim(s) against the `RolePermissions` table (joined to `Permissions.Code`) via `AppDbContext` on every request.
- `Users.Role` is free text; roles are not an enum. JWT is issued in `Services/AuthService.cs` with a `ClaimTypes.Role` claim, 2-hour expiry, and passwords hashed with BCrypt.

**Current phase:** new Thing-Tag features are being built without permission policies. They rely only on the fallback "authenticated user" policy. Per-endpoint permissions will be added for all features together once they're all built. Until then, don't add `Permissions.cs` constants, `[Authorize(Policy = ...)]` attributes or permission seed scripts for new features.

Adding a new protected endpoint (once permissions are being applied) means: add a constant to `Permissions.cs`, decorate the action, and add a SQL script that inserts the `Permissions` row and the `RolePermissions` grants (see `Scripts/005_permissions_seed.sql`). Granting/revoking access for an existing permission is purely a data change.

### Configuration

`appsettings.json` holds `ConnectionStrings:DefaultConnection` and `Jwt:Key`/`Issuer`/`Audience`, read both in `Program.cs` (validation) and `AuthService` (token signing).

## Repo notes

- `.gitignore` excludes `bin/`, `obj/`, IDE files, `.DS_Store` and dotnet tool state (`.local/`, `Library/`).
- `README.md` is the human-facing documentation (features, API overview, setup, scripts, testing). Update it together with this file whenever features, endpoints, scripts or setup steps change.
- Package versions are mixed: .NET 10 / ASP.NET Core 10.x packages alongside EF Core 9.x and Npgsql 9.x.
- Some comments in `Program.cs` are in Thai.
- Commit messages follow a Conventional Commits-like `type : message` style (e.g. `feat : Auth`, `refactor : README`).
