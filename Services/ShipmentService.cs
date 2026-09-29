using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class ShipmentService : IShipmentService
{
    private const string TrackingNumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // Event types recorded automatically; seeded by Scripts/011 and Scripts/021.
    private const string DeliveredEventCode = "DELIVERED";
    private const string CustomsHoldEventCode = "CUSTOMS_HOLD";
    private const string CustomsClearedEventCode = "CUSTOMS_CLEARED";

    private static readonly Dictionary<TransportMode, (string Departed, string Arrived)> LegEventCodes = new()
    {
        [TransportMode.Road] = ("VEHICLE_DEPARTED", "VEHICLE_ARRIVED"),
        [TransportMode.Courier] = ("VEHICLE_DEPARTED", "VEHICLE_ARRIVED"),
        [TransportMode.Rail] = ("TRAIN_DEPARTED", "TRAIN_ARRIVED"),
        [TransportMode.Air] = ("FLIGHT_DEPARTED", "FLIGHT_ARRIVED"),
        [TransportMode.Sea] = ("VESSEL_DEPARTED", "VESSEL_ARRIVED")
    };

    private static readonly Dictionary<TransportMode, TransportDocumentType> DefaultDocumentTypes = new()
    {
        [TransportMode.Road] = TransportDocumentType.RoadConsignmentNote,
        [TransportMode.Courier] = TransportDocumentType.CourierWaybill,
        [TransportMode.Rail] = TransportDocumentType.RailWaybill,
        [TransportMode.Air] = TransportDocumentType.AirWaybill,
        [TransportMode.Sea] = TransportDocumentType.BillOfLading
    };

    private readonly AppDbContext _context;
    private readonly ITrackingEventRecorder _recorder;
    private readonly ICurrentUser _currentUser;

    public ShipmentService(AppDbContext context, ITrackingEventRecorder recorder, ICurrentUser currentUser)
    {
        _context = context;
        _recorder = recorder;
        _currentUser = currentUser;
    }

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<ShipmentResponse>> GetShipmentsAsync(ShipmentQuery query)
    {
        var shipments = _context.Shipments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = QueryHelpers.ContainsPattern(query.Search);
            shipments = shipments.Where(s =>
                EF.Functions.ILike(s.TrackingNumber, pattern) ||
                (s.Reference != null && EF.Functions.ILike(s.Reference, pattern)) ||
                s.Legs.Any(l =>
                    (l.DocumentNumber != null && EF.Functions.ILike(l.DocumentNumber, pattern)) ||
                    (l.VoyageNumber != null && EF.Functions.ILike(l.VoyageNumber, pattern))));
        }
        if (query.Status.HasValue)
        {
            shipments = shipments.Where(s => s.Status == query.Status.Value);
        }
        if (query.CustomsStatus.HasValue)
        {
            shipments = shipments.Where(s => s.CustomsStatus == query.CustomsStatus.Value);
        }
        if (query.SenderPartyId.HasValue)
        {
            shipments = shipments.Where(s => s.SenderPartyId == query.SenderPartyId);
        }
        if (query.ReceiverPartyId.HasValue)
        {
            shipments = shipments.Where(s => s.ReceiverPartyId == query.ReceiverPartyId);
        }
        if (query.OriginLocationId.HasValue)
        {
            shipments = shipments.Where(s => s.OriginLocationId == query.OriginLocationId);
        }
        if (query.DestinationLocationId.HasValue)
        {
            shipments = shipments.Where(s => s.DestinationLocationId == query.DestinationLocationId);
        }
        if (query.Mode.HasValue)
        {
            shipments = shipments.Where(s => s.Legs.Any(l => l.Mode == query.Mode.Value));
        }

        var totalCount = await shipments.CountAsync();
        var pageIds = await shipments
            .OrderByDescending(s => s.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => s.Id)
            .ToListAsync();

        var page = await ShipmentsWithDetails().AsNoTracking()
            .Where(s => pageIds.Contains(s.Id))
            .ToListAsync();
        var itemCounts = await _context.ShipmentItems
            .Where(si => pageIds.Contains(si.ShipmentId))
            .GroupBy(si => si.ShipmentId)
            .Select(g => new { ShipmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ShipmentId, x => x.Count);

        return new PagedResult<ShipmentResponse>
        {
            Items = page
                .OrderByDescending(s => s.Id)
                .Select(s => ShipmentResponse.From(s, itemCounts.GetValueOrDefault(s.Id)))
                .ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ShipmentResponse?> GetShipmentByIdAsync(int id)
    {
        var shipment = await ShipmentsWithDetails().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        return shipment == null ? null : await ToResponseAsync(shipment);
    }

    public async Task<ShipmentResponse?> GetShipmentByTrackingNumberAsync(string trackingNumber)
    {
        var normalized = QueryHelpers.NormalizeCode(trackingNumber);
        var shipment = await ShipmentsWithDetails().AsNoTracking().FirstOrDefaultAsync(s => s.TrackingNumber == normalized);
        return shipment == null ? null : await ToResponseAsync(shipment);
    }

    public async Task<IReadOnlyList<TrackedItemResponse>?> GetItemsAsync(int id)
    {
        if (!await _context.Shipments.AnyAsync(s => s.Id == id)) return null;

        var items = await _context.TrackedItems.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.CurrentLocation)
            .Include(i => i.OwnerParty)
            .Include(i => i.CurrentContainer)
            .Where(i => _context.ShipmentItems.Any(si => si.ShipmentId == id && si.TrackedItemId == i.Id))
            .OrderBy(i => i.TagCode)
            .ToListAsync();
        return items.Select(TrackedItemResponse.From).ToList();
    }

    // --------------------------------------------------------- create / update

    public async Task<ShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request)
    {
        var trackingNumber = string.IsNullOrWhiteSpace(request.TrackingNumber)
            ? await GenerateTrackingNumberAsync()
            : QueryHelpers.NormalizeCode(request.TrackingNumber);
        if (await _context.Shipments.AnyAsync(s => s.TrackingNumber == trackingNumber))
        {
            throw new ConflictException($"Tracking number already exists: {trackingNumber}");
        }

        var now = DateTime.UtcNow;
        var shipment = new Shipment
        {
            TrackingNumber = trackingNumber,
            Status = ShipmentStatus.Planned,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.Username
        };
        await ApplyFieldsAsync(shipment, request);
        _context.Shipments.Add(shipment);

        if ((request.TrackedItemIds?.Count ?? 0) + (request.TagCodes?.Count ?? 0) + (request.ContainerIds?.Count ?? 0) > 0)
        {
            await AddItemsToShipmentAsync(shipment, request.TrackedItemIds, request.TagCodes, request.ContainerIds);
        }

        await _context.SaveChangesOrConflictAsync($"Tracking number already exists: {trackingNumber}");
        return (await GetShipmentByIdAsync(shipment.Id))!;
    }

    // Full edit (including the route) is only possible before anything has departed.
    public async Task<ShipmentResponse?> UpdateShipmentAsync(int id, UpdateShipmentRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "edited", ShipmentStatus.Planned);

        await ApplyFieldsAsync(shipment, request);
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    public async Task<ShipmentResponse?> UpdateLegAsync(int id, int legId, UpdateShipmentLegRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        var leg = shipment?.Legs.FirstOrDefault(l => l.Id == legId);
        if (shipment == null || leg == null) return null;

        EnsureStatus(shipment, "edited", ShipmentStatus.Planned, ShipmentStatus.InTransit);
        if (leg.ActualDeparture.HasValue)
        {
            throw new ConflictException($"Leg {leg.Sequence} has already departed and can't be changed.");
        }

        var (carrierId, documentType) = await ValidateTransportAsync(
            leg.Mode, request.CarrierId, request.VehicleId, leg.CarrierId, leg.VehicleId,
            request.DocumentType, request.DocumentNumber, $"Leg {leg.Sequence}: ");

        leg.CarrierId = carrierId;
        leg.VehicleId = request.VehicleId;
        leg.VehicleName = QueryHelpers.NullIfBlank(request.VehicleName);
        leg.VoyageNumber = QueryHelpers.NullIfBlank(request.VoyageNumber)?.ToUpperInvariant();
        leg.PlannedDeparture = request.PlannedDeparture?.UtcDateTime;
        leg.PlannedArrival = request.PlannedArrival?.UtcDateTime;
        leg.DocumentType = documentType;
        leg.DocumentNumber = QueryHelpers.NullIfBlank(request.DocumentNumber)?.ToUpperInvariant();
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    // ------------------------------------------------------------------- items

    public async Task<ShipmentResponse?> AddItemsAsync(int id, ShipmentItemsRequest request)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "given new items", ShipmentStatus.Planned);

        await AddItemsToShipmentAsync(shipment, request.TrackedItemIds, request.TagCodes, request.ContainerIds);
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    public async Task<ShipmentResponse?> RemoveItemAsync(int id, int trackedItemId)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "changed", ShipmentStatus.Planned);

        var link = await _context.ShipmentItems.FindAsync(id, trackedItemId);
        if (link == null)
        {
            throw new BusinessRuleException($"Item {trackedItemId} is not in shipment {shipment.TrackingNumber}.");
        }

        _context.ShipmentItems.Remove(link);
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    // -------------------------------------------------------------- operations

    public async Task<ShipmentResponse?> DepartLegAsync(int id, int legId, LegMovementRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        var leg = shipment?.Legs.FirstOrDefault(l => l.Id == legId);
        if (shipment == null || leg == null) return null;

        EnsureStatus(shipment, "dispatched", ShipmentStatus.Planned, ShipmentStatus.InTransit);
        if (leg.ActualDeparture.HasValue)
        {
            throw new ConflictException($"Leg {leg.Sequence} has already departed.");
        }
        if (shipment.CustomsStatus == CustomsStatus.Hold)
        {
            throw new ConflictException("Shipment is held by customs.");
        }

        var previous = shipment.Legs.Where(l => l.Sequence < leg.Sequence).OrderBy(l => l.Sequence).ToList();
        var notArrived = previous.FirstOrDefault(l => !l.ActualArrival.HasValue);
        if (notArrived != null)
        {
            throw new BusinessRuleException($"Leg {notArrived.Sequence} must arrive before leg {leg.Sequence} departs.");
        }

        var departedAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;
        var previousArrival = previous.LastOrDefault()?.ActualArrival;
        if (previousArrival.HasValue && departedAt < previousArrival.Value)
        {
            throw new BusinessRuleException("Departure can't be earlier than the previous leg's arrival.");
        }

        leg.ActualDeparture = departedAt;
        shipment.Status = ShipmentStatus.InTransit;
        shipment.UpdatedAt = DateTime.UtcNow;

        await RecordForShipmentAsync(shipment, LegEventCodes[leg.Mode].Departed,
            leg.OriginLocationId, departedAt, request.Note, leg.Id, isSystemManaged: true);

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    public async Task<ShipmentResponse?> ArriveLegAsync(int id, int legId, LegMovementRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        var leg = shipment?.Legs.FirstOrDefault(l => l.Id == legId);
        if (shipment == null || leg == null) return null;

        EnsureStatus(shipment, "updated", ShipmentStatus.InTransit);
        if (!leg.ActualDeparture.HasValue)
        {
            throw new BusinessRuleException($"Leg {leg.Sequence} hasn't departed yet.");
        }
        if (leg.ActualArrival.HasValue)
        {
            throw new ConflictException($"Leg {leg.Sequence} has already arrived.");
        }

        var arrivedAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;
        if (arrivedAt < leg.ActualDeparture.Value)
        {
            throw new BusinessRuleException("Arrival can't be earlier than departure.");
        }

        leg.ActualArrival = arrivedAt;
        shipment.UpdatedAt = DateTime.UtcNow;

        await RecordForShipmentAsync(shipment, LegEventCodes[leg.Mode].Arrived,
            leg.DestinationLocationId, arrivedAt, request.Note, leg.Id, isSystemManaged: true);

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    public async Task<ShipmentResponse?> UpdateCustomsAsync(int id, CustomsUpdateRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "updated", ShipmentStatus.Planned, ShipmentStatus.InTransit);

        var status = request.Status!.Value;
        shipment.CustomsStatus = status;
        shipment.UpdatedAt = DateTime.UtcNow;

        var eventCode = status switch
        {
            CustomsStatus.Hold => CustomsHoldEventCode,
            CustomsStatus.Cleared => CustomsClearedEventCode,
            _ => null
        };
        if (eventCode != null)
        {
            await RecordForShipmentAsync(shipment, eventCode,
                request.LocationId ?? CurrentLocationId(shipment),
                request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow,
                request.Note, null, isSystemManaged: true, reasonCode: request.ReasonCode);
        }

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    // Any other event (e.g. OUT_FOR_DELIVERY, DAMAGED) for every item in the shipment.
    public async Task<EventsRecordedResponse?> RecordEventAsync(int id, EventDetails request)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return null;
        if (shipment.Status == ShipmentStatus.Cancelled)
        {
            throw new ConflictException("Shipment is cancelled.");
        }

        var count = await RecordForShipmentAsync(shipment, request.EventTypeCode, request.LocationId,
            request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow, request.Note, null, isSystemManaged: false,
            reasonCode: request.ReasonCode, latitude: request.Latitude, longitude: request.Longitude);

        await _context.SaveChangesAsync();
        return new EventsRecordedResponse(QueryHelpers.NormalizeCode(request.EventTypeCode), count);
    }

    public async Task<ShipmentResponse?> DeliverAsync(int id, DeliverShipmentRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "delivered", ShipmentStatus.Planned, ShipmentStatus.InTransit);

        var notArrived = shipment.Legs.OrderBy(l => l.Sequence).FirstOrDefault(l => !l.ActualArrival.HasValue);
        if (notArrived != null)
        {
            throw new BusinessRuleException($"Leg {notArrived.Sequence} hasn't arrived yet.");
        }
        if (shipment.CustomsStatus is CustomsStatus.Pending or CustomsStatus.InProgress or CustomsStatus.Hold)
        {
            throw new BusinessRuleException($"Customs is not cleared (status: {shipment.CustomsStatus}).");
        }

        var deliveredAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(request.ReceivedBy)
            ? request.Note
            : $"Received by {request.ReceivedBy.Trim()}" + (string.IsNullOrWhiteSpace(request.Note) ? "" : $". {request.Note}");

        shipment.Status = ShipmentStatus.Delivered;
        shipment.DeliveredAt = deliveredAt;
        shipment.UpdatedAt = DateTime.UtcNow;

        await RecordForShipmentAsync(shipment, DeliveredEventCode, shipment.DestinationLocationId, deliveredAt, note, null,
            isSystemManaged: true);

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    public async Task<ShipmentResponse?> CancelAsync(int id)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "cancelled", ShipmentStatus.Planned);

        shipment.Status = ShipmentStatus.Cancelled;
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    // ----------------------------------------------------------------- helpers

    private IQueryable<Shipment> ShipmentsWithDetails() =>
        _context.Shipments
            .Include(s => s.SenderParty)
            .Include(s => s.ReceiverParty)
            .Include(s => s.OriginLocation)
            .Include(s => s.DestinationLocation)
            .Include(s => s.Legs).ThenInclude(l => l.Carrier)
            .Include(s => s.Legs).ThenInclude(l => l.Vehicle)
            .Include(s => s.Legs).ThenInclude(l => l.OriginLocation)
            .Include(s => s.Legs).ThenInclude(l => l.DestinationLocation)
            .AsSplitQuery();

    private async Task<ShipmentResponse> ToResponseAsync(Shipment shipment)
    {
        var itemCount = await _context.ShipmentItems.CountAsync(si => si.ShipmentId == shipment.Id);
        return ShipmentResponse.From(shipment, itemCount);
    }

    private static void EnsureStatus(Shipment shipment, string action, params ShipmentStatus[] allowed)
    {
        if (!allowed.Contains(shipment.Status))
        {
            throw new ConflictException(
                $"A {shipment.Status} shipment can't be {action} (allowed when: {string.Join(", ", allowed)}).");
        }
    }

    // Last place the shipment reached: destination of the latest arrived leg, else the origin.
    private static int CurrentLocationId(Shipment shipment) =>
        shipment.Legs
            .Where(l => l.ActualArrival.HasValue)
            .OrderByDescending(l => l.Sequence)
            .Select(l => (int?)l.DestinationLocationId)
            .FirstOrDefault() ?? shipment.OriginLocationId;

    // isSystemManaged: the event mirrors this shipment's own state (legs, customs,
    // delivery), so it can't be voided on its own.
    private async Task<int> RecordForShipmentAsync(
        Shipment shipment, string eventTypeCode, int? locationId, DateTime occurredAt, string? note,
        int? legId, bool isSystemManaged, string? reasonCode = null, decimal? latitude = null, decimal? longitude = null)
    {
        var items = await _context.TrackedItems
            .Where(i => _context.ShipmentItems.Any(si => si.ShipmentId == shipment.Id && si.TrackedItemId == i.Id))
            .ToListAsync();
        if (items.Count == 0)
        {
            throw new BusinessRuleException($"Shipment {shipment.TrackingNumber} has no items.");
        }

        var eventType = await _recorder.GetEventTypeAsync(eventTypeCode);
        await _recorder.AddEventsAsync(items, eventType, new EventContext(
            locationId, occurredAt, EventSource.Shipment, note, latitude, longitude,
            ShipmentId: shipment.Id, ShipmentLegId: legId, ReasonCode: reasonCode, IsSystemManaged: isSystemManaged));
        return items.Count;
    }

    private async Task AddItemsToShipmentAsync(
        Shipment shipment, List<int>? ids, List<string>? tagCodes, List<int>? containerIds)
    {
        var items = new List<TrackedItem>();
        if ((ids?.Count ?? 0) + (tagCodes?.Count ?? 0) > 0)
        {
            items.AddRange(await _recorder.FindItemsAsync(ids, tagCodes));
        }
        if (containerIds?.Count > 0)
        {
            var distinct = containerIds.Distinct().ToList();
            var found = await _context.Containers.Where(c => distinct.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            var missing = distinct.Except(found).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessRuleException($"Containers not found: {string.Join(", ", missing)}");
            }
            items.AddRange(await _recorder.FindItemsInContainersAsync(distinct));
        }

        items = items.DistinctBy(i => i.Id).ToList();
        if (items.Count == 0)
        {
            throw new BusinessRuleException("No items to add (the containers are empty).");
        }

        var alreadyIn = shipment.Id == 0
            ? []
            : await _context.ShipmentItems.Where(si => si.ShipmentId == shipment.Id).Select(si => si.TrackedItemId).ToListAsync();
        items = items.Where(i => !alreadyIn.Contains(i.Id)).ToList();

        // An item can travel in only one open shipment at a time.
        var itemIds = items.Select(i => i.Id).ToList();
        var busy = await _context.ShipmentItems
            .Where(si => itemIds.Contains(si.TrackedItemId) && si.ShipmentId != shipment.Id &&
                         (si.Shipment.Status == ShipmentStatus.Planned || si.Shipment.Status == ShipmentStatus.InTransit))
            .Select(si => si.TrackedItem.TagCode + " (" + si.Shipment.TrackingNumber + ")")
            .ToListAsync();
        if (busy.Count > 0)
        {
            throw new ConflictException($"Items are already in another open shipment: {string.Join(", ", busy)}");
        }

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            _context.ShipmentItems.Add(new ShipmentItem { Shipment = shipment, TrackedItemId = item.Id, AddedAt = now });
        }
    }

    private async Task ApplyFieldsAsync(Shipment shipment, ShipmentFields fields)
    {
        var isNew = shipment.Id == 0;
        await ReferenceResolver.ResolveRequiredAsync(_context.Parties.AsNoTracking(), fields.SenderPartyId!.Value,
            isNew ? null : shipment.SenderPartyId, "Sender");
        await ReferenceResolver.ResolveRequiredAsync(_context.Parties.AsNoTracking(), fields.ReceiverPartyId!.Value,
            isNew ? null : shipment.ReceiverPartyId, "Receiver");
        var origin = await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(), fields.OriginLocationId!.Value,
            isNew ? null : shipment.OriginLocationId, "Origin");
        var destination = await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(), fields.DestinationLocationId!.Value,
            isNew ? null : shipment.DestinationLocationId, "Destination");

        await ApplyLegsAsync(shipment, fields.Legs, origin.Id, destination.Id);

        shipment.Reference = QueryHelpers.NullIfBlank(fields.Reference);
        shipment.SenderPartyId = fields.SenderPartyId.Value;
        shipment.ReceiverPartyId = fields.ReceiverPartyId.Value;
        shipment.OriginLocationId = origin.Id;
        shipment.DestinationLocationId = destination.Id;
        shipment.Incoterm = QueryHelpers.NullIfBlank(fields.Incoterm)?.ToUpperInvariant();
        shipment.CustomsStatus = fields.CustomsStatus
            ?? (origin.Country != destination.Country ? CustomsStatus.Pending : CustomsStatus.NotRequired);
        shipment.PlannedPickupAt = fields.PlannedPickupAt?.UtcDateTime;
        shipment.Notes = QueryHelpers.NullIfBlank(fields.Notes);
    }

    // Legs are updated in place by sequence so leg ids stay stable.
    private async Task ApplyLegsAsync(Shipment shipment, List<ShipmentLegRequest> requests, int originId, int destinationId)
    {
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var prefix = $"Leg {index + 1}: ";

            var expectedOrigin = index == 0 ? originId : requests[index - 1].DestinationLocationId!.Value;
            if (request.OriginLocationId != expectedOrigin)
            {
                throw new BusinessRuleException(index == 0
                    ? $"{prefix}must start at the shipment origin."
                    : $"{prefix}must start where leg {index} ends.");
            }
            if (index == requests.Count - 1 && request.DestinationLocationId != destinationId)
            {
                throw new BusinessRuleException($"{prefix}the last leg must end at the shipment destination.");
            }
            if (index > 0 && request.PlannedDeparture.HasValue && requests[index - 1].PlannedArrival.HasValue &&
                request.PlannedDeparture < requests[index - 1].PlannedArrival)
            {
                throw new BusinessRuleException($"{prefix}PlannedDeparture is before leg {index}'s PlannedArrival.");
            }

            var existing = shipment.Legs.FirstOrDefault(l => l.Sequence == index + 1);
            await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(),
                request.OriginLocationId!.Value, existing?.OriginLocationId, "Location", prefix);
            await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(),
                request.DestinationLocationId!.Value, existing?.DestinationLocationId, "Location", prefix);

            var mode = request.Mode!.Value;
            var (carrierId, documentType) = await ValidateTransportAsync(
                mode, request.CarrierId, request.VehicleId, existing?.CarrierId, existing?.VehicleId,
                request.DocumentType, request.DocumentNumber, prefix);

            var leg = existing ?? new ShipmentLeg { Sequence = index + 1 };
            leg.Mode = mode;
            leg.OriginLocationId = request.OriginLocationId.Value;
            leg.DestinationLocationId = request.DestinationLocationId.Value;
            leg.CarrierId = carrierId;
            leg.VehicleId = request.VehicleId;
            leg.VehicleName = QueryHelpers.NullIfBlank(request.VehicleName);
            leg.VoyageNumber = QueryHelpers.NullIfBlank(request.VoyageNumber)?.ToUpperInvariant();
            leg.PlannedDeparture = request.PlannedDeparture?.UtcDateTime;
            leg.PlannedArrival = request.PlannedArrival?.UtcDateTime;
            leg.DocumentType = documentType;
            leg.DocumentNumber = QueryHelpers.NullIfBlank(request.DocumentNumber)?.ToUpperInvariant();
            if (existing == null)
            {
                shipment.Legs.Add(leg);
            }
        }

        foreach (var removed in shipment.Legs.Where(l => l.Sequence > requests.Count).ToList())
        {
            shipment.Legs.Remove(removed);
            _context.ShipmentLegs.Remove(removed);
        }
    }

    // Checks carrier/vehicle/document against the leg's mode. Returns the carrier
    // (defaulted from the vehicle) and the document type (defaulted from the mode).
    private async Task<(int? CarrierId, TransportDocumentType? DocumentType)> ValidateTransportAsync(
        TransportMode mode, int? carrierId, int? vehicleId, int? currentCarrierId, int? currentVehicleId,
        TransportDocumentType? documentType, string? documentNumber, string prefix)
    {
        var vehicle = await ReferenceResolver.ResolveAsync(
            _context.Vehicles.AsNoTracking(), vehicleId, currentVehicleId, "Vehicle", prefix);
        if (vehicle != null)
        {
            if (!SameFamily(vehicle.Mode, mode))
            {
                throw new BusinessRuleException($"{prefix}vehicle {vehicle.Code} is a {vehicle.Mode} vehicle, not {mode}.");
            }
            if (carrierId.HasValue && vehicle.CarrierId.HasValue && carrierId != vehicle.CarrierId)
            {
                throw new BusinessRuleException($"{prefix}vehicle {vehicle.Code} belongs to a different carrier.");
            }
            carrierId ??= vehicle.CarrierId;
        }

        var carrier = await ReferenceResolver.ResolveAsync(
            _context.Carriers.AsNoTracking(), carrierId, currentCarrierId, "Carrier", prefix);
        if (carrier != null && !carrier.Modes.Any(m => SameFamily(Enum.Parse<TransportMode>(m), mode)))
        {
            throw new BusinessRuleException($"{prefix}carrier {carrier.Code} does not operate {mode} transport.");
        }

        if (documentType.HasValue)
        {
            var expected = DefaultDocumentTypes[mode];
            var roadFamily = SameFamily(mode, TransportMode.Road) &&
                             documentType is TransportDocumentType.RoadConsignmentNote or TransportDocumentType.CourierWaybill;
            if (documentType != expected && !roadFamily)
            {
                throw new BusinessRuleException($"{prefix}{documentType} is not a {mode} document (expected {expected}).");
            }
        }
        else if (!string.IsNullOrWhiteSpace(documentNumber))
        {
            documentType = DefaultDocumentTypes[mode];
        }

        return (carrierId, documentType);
    }

    // Road and Courier are interchangeable (a courier van is a road vehicle).
    private static bool SameFamily(TransportMode a, TransportMode b) =>
        a == b || (IsRoad(a) && IsRoad(b));

    private static bool IsRoad(TransportMode mode) => mode is TransportMode.Road or TransportMode.Courier;

    private async Task<string> GenerateTrackingNumberAsync()
    {
        while (true)
        {
            var number = $"TS{DateTime.UtcNow:yyMMdd}{RandomNumberGenerator.GetString(TrackingNumberAlphabet, 6)}";
            if (!await _context.Shipments.AnyAsync(s => s.TrackingNumber == number))
            {
                return number;
            }
        }
    }
}
