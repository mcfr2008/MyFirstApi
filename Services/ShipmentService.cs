using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public partial class ShipmentService : IShipmentService
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
    private readonly IFileStorage _fileStorage;

    public ShipmentService(
        AppDbContext context, ITrackingEventRecorder recorder, ICurrentUser currentUser, IFileStorage fileStorage)
    {
        _context = context;
        _recorder = recorder;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    // ------------------------------------------------------------------ queries

    public async Task<PagedResult<ShipmentResponse>> GetShipmentsAsync(ShipmentQuery query)
    {
        var shipments = _context.Shipments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Two indexed lookups combined with UNION: an OR across Shipments and an
            // EXISTS on ShipmentLegs can't use the trigram indexes and scans every shipment.
            var pattern = QueryHelpers.ContainsPattern(query.Search);
            var matchingIds = _context.Shipments
                .Where(s => EF.Functions.ILike(s.TrackingNumber, pattern) ||
                            (s.Reference != null && EF.Functions.ILike(s.Reference, pattern)))
                .Select(s => s.Id)
                .Union(_context.ShipmentLegs
                    .Where(l => (l.DocumentNumber != null && EF.Functions.ILike(l.DocumentNumber, pattern)) ||
                                (l.VoyageNumber != null && EF.Functions.ILike(l.VoyageNumber, pattern)))
                    .Select(l => l.ShipmentId));
            shipments = shipments.Where(s => matchingIds.Contains(s.Id));
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

        var withProof = await _context.ProofsOfDelivery
            .Where(p => pageIds.Contains(p.ShipmentId))
            .Select(p => p.ShipmentId)
            .ToListAsync();

        return new PagedResult<ShipmentResponse>
        {
            Items = page
                .OrderByDescending(s => s.Id)
                .Select(s => ShipmentResponse.From(s, itemCounts.GetValueOrDefault(s.Id), withProof.Contains(s.Id)))
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
            throw Errors.TrackingNumberExists(trackingNumber);
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

        await _context.SaveChangesOrConflictAsync(Errors.TrackingNumberExists(trackingNumber));
        return (await GetShipmentByIdAsync(shipment.Id))!;
    }

    // Full edit (including the route) is only possible before anything has departed.
    public async Task<ShipmentResponse?> UpdateShipmentAsync(int id, UpdateShipmentRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "edit", ShipmentStatus.Planned);

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

        EnsureStatus(shipment, "editLeg", ShipmentStatus.Planned, ShipmentStatus.InTransit);
        if (leg.ActualDeparture.HasValue)
        {
            throw Errors.LegAlreadyDeparted(leg.Sequence);
        }

        var (carrierId, documentType) = await ValidateTransportAsync(
            leg.Mode, request.CarrierId, request.VehicleId, leg.CarrierId, leg.VehicleId,
            request.DocumentType, request.DocumentNumber, ErrorScope.Leg(leg.Sequence));

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
        EnsureStatus(shipment, "addItems", ShipmentStatus.Planned);

        await AddItemsToShipmentAsync(shipment, request.TrackedItemIds, request.TagCodes, request.ContainerIds);
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await GetShipmentByIdAsync(id);
    }

    public async Task<ShipmentResponse?> RemoveItemAsync(int id, int trackedItemId)
    {
        var shipment = await _context.Shipments.FindAsync(id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "removeItem", ShipmentStatus.Planned);

        var link = await _context.ShipmentItems.FindAsync(id, trackedItemId);
        if (link == null)
        {
            throw Errors.ItemNotInShipment(trackedItemId, shipment.TrackingNumber);
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

        EnsureStatus(shipment, "departLeg", ShipmentStatus.Planned, ShipmentStatus.InTransit);
        if (leg.ActualDeparture.HasValue)
        {
            throw Errors.LegAlreadyDeparted(leg.Sequence);
        }
        if (shipment.CustomsStatus == CustomsStatus.Hold)
        {
            throw Errors.ShipmentOnCustomsHold();
        }

        var previous = shipment.Legs.Where(l => l.Sequence < leg.Sequence).OrderBy(l => l.Sequence).ToList();
        var notArrived = previous.FirstOrDefault(l => !l.ActualArrival.HasValue);
        if (notArrived != null)
        {
            throw Errors.PreviousLegNotArrived(notArrived.Sequence, leg.Sequence);
        }

        var departedAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;
        var previousArrival = previous.LastOrDefault()?.ActualArrival;
        if (previousArrival.HasValue && departedAt < previousArrival.Value)
        {
            throw Errors.DepartureBeforePreviousArrival();
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

        EnsureStatus(shipment, "arriveLeg", ShipmentStatus.InTransit);
        if (!leg.ActualDeparture.HasValue)
        {
            throw Errors.LegNotDeparted(leg.Sequence);
        }
        if (leg.ActualArrival.HasValue)
        {
            throw Errors.LegAlreadyArrived(leg.Sequence);
        }

        var arrivedAt = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;
        if (arrivedAt < leg.ActualDeparture.Value)
        {
            throw Errors.ArrivalBeforeDeparture();
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
        EnsureStatus(shipment, "updateCustoms", ShipmentStatus.Planned, ShipmentStatus.InTransit);

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
            throw Errors.ShipmentCancelled();
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
        if (shipment.RequiresSignature)
        {
            throw Errors.SignatureRequired();
        }
        EnsureDeliverable(shipment);

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
        EnsureStatus(shipment, "cancel", ShipmentStatus.Planned);

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
        var hasProof = await _context.ProofsOfDelivery.AnyAsync(p => p.ShipmentId == shipment.Id);
        return ShipmentResponse.From(shipment, itemCount, hasProof);
    }

    // Shared by /deliver and proof of delivery.
    private static void EnsureDeliverable(Shipment shipment)
    {
        EnsureStatus(shipment, "deliver", ShipmentStatus.Planned, ShipmentStatus.InTransit);

        var notArrived = shipment.Legs.OrderBy(l => l.Sequence).FirstOrDefault(l => !l.ActualArrival.HasValue);
        if (notArrived != null)
        {
            throw Errors.LegNotArrived(notArrived.Sequence);
        }
        if (shipment.CustomsStatus is CustomsStatus.Pending or CustomsStatus.InProgress or CustomsStatus.Hold)
        {
            throw Errors.CustomsNotCleared(shipment.CustomsStatus);
        }
    }

    private static void EnsureStatus(Shipment shipment, string action, params ShipmentStatus[] allowed)
    {
        if (!allowed.Contains(shipment.Status))
        {
            throw Errors.ShipmentStatusNotAllowed(action, shipment.Status, allowed.Cast<object>());
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
        var items = await LoadShipmentItemsAsync(shipment);
        if (items.Count == 0)
        {
            throw Errors.ShipmentHasNoItems(shipment.TrackingNumber);
        }

        return await RecordForItemsAsync(shipment, items, eventTypeCode, locationId, occurredAt, note, legId,
            isSystemManaged, reasonCode, latitude, longitude);
    }

    // Tracked, so events can update their current state.
    private Task<List<TrackedItem>> LoadShipmentItemsAsync(Shipment shipment) =>
        _context.TrackedItems
            .Where(i => _context.ShipmentItems.Any(si => si.ShipmentId == shipment.Id && si.TrackedItemId == i.Id))
            .ToListAsync();

    private async Task<int> RecordForItemsAsync(
        Shipment shipment, List<TrackedItem> items, string eventTypeCode, int? locationId, DateTime occurredAt,
        string? note, int? legId, bool isSystemManaged, string? reasonCode = null,
        decimal? latitude = null, decimal? longitude = null)
    {
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
                throw Errors.ContainersNotFound(missing);
            }
            items.AddRange(await _recorder.FindItemsInContainersAsync(distinct));
        }

        items = items.DistinctBy(i => i.Id).ToList();
        if (items.Count == 0)
        {
            throw Errors.NoItemsToAdd();
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
            throw Errors.ItemsInOpenShipment(busy);
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
            isNew ? null : shipment.SenderPartyId, "senderPartyId");
        await ReferenceResolver.ResolveRequiredAsync(_context.Parties.AsNoTracking(), fields.ReceiverPartyId!.Value,
            isNew ? null : shipment.ReceiverPartyId, "receiverPartyId");
        var origin = await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(), fields.OriginLocationId!.Value,
            isNew ? null : shipment.OriginLocationId, "originLocationId");
        var destination = await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(), fields.DestinationLocationId!.Value,
            isNew ? null : shipment.DestinationLocationId, "destinationLocationId");

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
        shipment.RequiresSignature = fields.RequiresSignature ?? (isNew || shipment.RequiresSignature);
        shipment.Notes = QueryHelpers.NullIfBlank(fields.Notes);
    }

    // Legs are updated in place by sequence so leg ids stay stable.
    private async Task ApplyLegsAsync(Shipment shipment, List<ShipmentLegRequest> requests, int originId, int destinationId)
    {
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            var scope = ErrorScope.Leg(index + 1);

            var expectedOrigin = index == 0 ? originId : requests[index - 1].DestinationLocationId!.Value;
            if (request.OriginLocationId != expectedOrigin)
            {
                throw (index == 0 ? Errors.LegMustStartAtOrigin() : Errors.LegNotContinuous(index)).In(scope);
            }
            if (index == requests.Count - 1 && request.DestinationLocationId != destinationId)
            {
                throw Errors.LastLegMustEndAtDestination().In(scope);
            }
            if (index > 0 && request.PlannedDeparture.HasValue && requests[index - 1].PlannedArrival.HasValue &&
                request.PlannedDeparture < requests[index - 1].PlannedArrival)
            {
                throw Errors.LegScheduleOverlap(index).In(scope);
            }

            var existing = shipment.Legs.FirstOrDefault(l => l.Sequence == index + 1);
            await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(),
                request.OriginLocationId!.Value, existing?.OriginLocationId, "originLocationId", scope);
            await ReferenceResolver.ResolveRequiredAsync(_context.Locations.AsNoTracking(),
                request.DestinationLocationId!.Value, existing?.DestinationLocationId, "destinationLocationId", scope);

            var mode = request.Mode!.Value;
            var (carrierId, documentType) = await ValidateTransportAsync(
                mode, request.CarrierId, request.VehicleId, existing?.CarrierId, existing?.VehicleId,
                request.DocumentType, request.DocumentNumber, scope);

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
        TransportDocumentType? documentType, string? documentNumber, ErrorScope scope)
    {
        var vehicle = await ReferenceResolver.ResolveAsync(
            _context.Vehicles.AsNoTracking(), vehicleId, currentVehicleId, "vehicleId", scope);
        if (vehicle != null)
        {
            if (!SameFamily(vehicle.Mode, mode))
            {
                throw Errors.VehicleModeMismatch(vehicle.Code, vehicle.Mode, mode).In(scope);
            }
            if (carrierId.HasValue && vehicle.CarrierId.HasValue && carrierId != vehicle.CarrierId)
            {
                throw Errors.VehicleCarrierMismatch(vehicle.Code).In(scope);
            }
            carrierId ??= vehicle.CarrierId;
        }

        var carrier = await ReferenceResolver.ResolveAsync(
            _context.Carriers.AsNoTracking(), carrierId, currentCarrierId, "carrierId", scope);
        if (carrier != null && !carrier.Modes.Any(m => SameFamily(Enum.Parse<TransportMode>(m), mode)))
        {
            throw Errors.CarrierModeNotSupported(carrier.Code, mode).In(scope);
        }

        if (documentType.HasValue)
        {
            var expected = DefaultDocumentTypes[mode];
            var roadFamily = SameFamily(mode, TransportMode.Road) &&
                             documentType is TransportDocumentType.RoadConsignmentNote or TransportDocumentType.CourierWaybill;
            if (documentType != expected && !roadFamily)
            {
                throw Errors.DocumentTypeMismatch(documentType, mode, expected).In(scope);
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
