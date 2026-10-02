using Microsoft.EntityFrameworkCore;
using MyFirstApi.Dtos;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Misroute detection. TrackingEventRecorder flags each scan whose location isn't on
// the item's open shipment route (TrackingEvent.OffRouteShipmentId). A shipment is
// misrouted *now* when an item's current location is such a flagged, non-voided scan
// and still isn't on the route (a later re-route may have added it). Items that
// were somewhere else before joining the shipment have no flagged scan, so they
// don't count.
public partial class ShipmentService
{
    public async Task<ShipmentRouteCheckResponse?> GetRouteCheckAsync(int id)
    {
        var shipment = await ShipmentsWithDetails().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;

        var legs = shipment.Legs.OrderBy(l => l.Sequence).ToList();
        var routePoints = new List<ReferenceSummary> { Summary(shipment.OriginLocation) };
        routePoints.AddRange(legs.Count > 0
            ? legs.Select(l => Summary(l.DestinationLocation))
            : [Summary(shipment.DestinationLocation)]);
        var onRoute = routePoints.Select(p => p.Id)
            .Concat(legs.Select(l => l.OriginLocationId))
            .ToHashSet();

        var offRouteEvents = await _context.TrackingEvents.AsNoTracking()
            .Where(e => e.OffRouteShipmentId == id && !e.IsVoided)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id)
            .Select(e => new OffRouteEventResponse
            {
                EventId = e.Id,
                OccurredAt = e.OccurredAt,
                Item = new ReferenceSummary(e.TrackedItem.Id, e.TrackedItem.TagCode, e.TrackedItem.Name),
                Location = new ReferenceSummary(e.Location!.Id, e.Location.Code, e.Location.Name),
                EventTypeCode = e.EventType.Code
            })
            .ToListAsync();

        var items = await _context.ShipmentItems.AsNoTracking()
            .Where(si => si.ShipmentId == id)
            .Select(si => new { si.TrackedItem.Id, si.TrackedItem.CurrentLocationId })
            .ToListAsync();
        var isOpen = shipment.Status is ShipmentStatus.Planned or ShipmentStatus.InTransit;
        var itemsOffRoute = !isOpen
            ? new List<OffRouteItemResponse>()
            : items
                .Where(i => i.CurrentLocationId.HasValue && !onRoute.Contains(i.CurrentLocationId.Value))
                .Select(i => offRouteEvents.FirstOrDefault(e => e.Item.Id == i.Id && e.Location.Id == i.CurrentLocationId))
                .Where(e => e != null)
                .Select(e => new OffRouteItemResponse { Item = e!.Item, CurrentLocation = e.Location, Since = e.OccurredAt })
                .ToList();

        return new ShipmentRouteCheckResponse
        {
            ShipmentId = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            Status = shipment.Status,
            IsOffRoute = itemsOffRoute.Count > 0,
            RoutePoints = routePoints,
            ItemsOffRoute = itemsOffRoute,
            OffRouteEvents = offRouteEvents
        };
    }

    // Ids of open shipments that are misrouted now (for GET /Shipments?offRoute=true).
    // Starts from the flagged scans (partial index), so it stays cheap.
    private IQueryable<int> MisroutedShipmentIds() =>
        from e in _context.TrackingEvents
        where e.OffRouteShipmentId != null && !e.IsVoided && e.LocationId != null
        join s in _context.Shipments on e.OffRouteShipmentId equals s.Id
        where (s.Status == ShipmentStatus.Planned || s.Status == ShipmentStatus.InTransit) &&
              e.TrackedItem.CurrentLocationId == e.LocationId &&
              s.Items.Any(si => si.TrackedItemId == e.TrackedItemId) &&
              s.OriginLocationId != e.LocationId &&
              s.DestinationLocationId != e.LocationId &&
              !s.Legs.Any(l => l.OriginLocationId == e.LocationId || l.DestinationLocationId == e.LocationId)
        select s.Id;

    private static ReferenceSummary Summary(Location location) => new(location.Id, location.Code, location.Name);
}
