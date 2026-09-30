using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Customer-facing tracking by shipment tracking number. Read-only; see
// PublicTrackingResponse for what is left out.
public class PublicTrackingService : IPublicTrackingService
{
    private readonly AppDbContext _context;

    public PublicTrackingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PublicTrackingResponse?> TrackAsync(string trackingNumber)
    {
        var normalized = QueryHelpers.NormalizeCode(trackingNumber);
        var shipment = await _context.Shipments.AsNoTracking()
            .Include(s => s.OriginLocation)
            .Include(s => s.DestinationLocation)
            .Include(s => s.Legs).ThenInclude(l => l.Carrier)
            .Include(s => s.Legs).ThenInclude(l => l.OriginLocation)
            .Include(s => s.Legs).ThenInclude(l => l.DestinationLocation)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.TrackingNumber == normalized);
        if (shipment == null) return null;

        var itemsAddedAt = await _context.ShipmentItems
            .Where(si => si.ShipmentId == shipment.Id)
            .ToDictionaryAsync(si => si.TrackedItemId, si => si.AddedAt);
        var signed = await _context.ProofsOfDelivery.AnyAsync(p => p.ShipmentId == shipment.Id);
        var legs = shipment.Legs.OrderBy(l => l.Sequence).ToList();

        return new PublicTrackingResponse
        {
            TrackingNumber = shipment.TrackingNumber,
            Status = shipment.Status,
            CustomsStatus = shipment.CustomsStatus,
            Origin = PublicLocationResponse.From(shipment.OriginLocation),
            Destination = PublicLocationResponse.From(shipment.DestinationLocation),
            PlannedPickupAt = shipment.PlannedPickupAt,
            EstimatedDeliveryAt = legs.LastOrDefault()?.PlannedArrival,
            DeliveredAt = shipment.DeliveredAt,
            SignedForDelivery = signed,
            TotalPieces = itemsAddedAt.Count,
            Legs = legs.Select(PublicLegResponse.From).ToList(),
            Events = await GetEventsAsync(shipment, itemsAddedAt)
        };
    }

    // The shipment's own events (departures, customs, delivery, ...) plus other
    // events of its items while they were in it (hub scans, damage reports, ...).
    // Voided events are left out.
    private async Task<List<PublicTrackingEventResponse>> GetEventsAsync(
        Shipment shipment, Dictionary<int, DateTime> itemsAddedAt)
    {
        var rows = await _context.TrackingEvents.AsNoTracking()
            .Where(e => e.ShipmentId == shipment.Id && !e.IsVoided)
            .Select(e => new EventRow(e.TrackedItemId, e.EventTypeId, e.OccurredAt, e.LocationId, e.ReasonCodeId))
            .ToListAsync();

        if (itemsAddedAt.Count > 0)
        {
            var itemIds = itemsAddedAt.Keys.ToList();
            var from = itemsAddedAt.Values.Min();
            // A closed shipment's window ends when it was delivered or cancelled.
            DateTime? until = shipment.Status switch
            {
                ShipmentStatus.Delivered => shipment.DeliveredAt,
                ShipmentStatus.Cancelled => shipment.UpdatedAt,
                _ => null
            };

            var itemEvents = _context.TrackingEvents.AsNoTracking()
                .Where(e => itemIds.Contains(e.TrackedItemId) && e.ShipmentId == null && !e.IsVoided &&
                            e.OccurredAt >= from);
            if (until.HasValue)
            {
                itemEvents = itemEvents.Where(e => e.OccurredAt <= until.Value);
            }

            var itemRows = await itemEvents
                .Select(e => new EventRow(e.TrackedItemId, e.EventTypeId, e.OccurredAt, e.LocationId, e.ReasonCodeId))
                .ToListAsync();
            rows.AddRange(itemRows.Where(r => r.OccurredAt >= itemsAddedAt[r.TrackedItemId]));
        }
        if (rows.Count == 0) return new();

        var eventTypeIds = rows.Select(r => r.EventTypeId).Distinct().ToList();
        var locationIds = rows.Where(r => r.LocationId.HasValue).Select(r => r.LocationId!.Value).Distinct().ToList();
        var reasonIds = rows.Where(r => r.ReasonCodeId.HasValue).Select(r => r.ReasonCodeId!.Value).Distinct().ToList();

        var eventTypes = await _context.EventTypes.AsNoTracking()
            .Where(t => eventTypeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var locations = await _context.Locations.AsNoTracking()
            .Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        var reasons = await _context.ReasonCodes.AsNoTracking()
            .Where(r => reasonIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        // A shipment operation records one event per item; show it once with a piece count.
        return rows
            .GroupBy(r => new { r.EventTypeId, r.OccurredAt, r.LocationId, r.ReasonCodeId })
            .OrderByDescending(g => g.Key.OccurredAt)
            .ThenByDescending(g => eventTypes[g.Key.EventTypeId].SortOrder)
            .Select(g =>
            {
                var type = eventTypes[g.Key.EventTypeId];
                var reason = g.Key.ReasonCodeId.HasValue ? reasons[g.Key.ReasonCodeId.Value] : null;
                return new PublicTrackingEventResponse
                {
                    OccurredAt = g.Key.OccurredAt,
                    EventCode = type.Code,
                    NameEn = type.NameEn,
                    NameTh = type.NameTh,
                    Location = g.Key.LocationId.HasValue
                        ? PublicLocationResponse.From(locations[g.Key.LocationId.Value])
                        : null,
                    ReasonCode = reason?.Code,
                    ReasonNameEn = reason?.NameEn,
                    ReasonNameTh = reason?.NameTh,
                    Pieces = g.Select(r => r.TrackedItemId).Distinct().Count()
                };
            })
            .ToList();
    }

    private record EventRow(int TrackedItemId, int EventTypeId, DateTime OccurredAt, int? LocationId, int? ReasonCodeId);
}
