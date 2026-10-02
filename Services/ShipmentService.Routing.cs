using Microsoft.EntityFrameworkCore;
using MyFirstApi.Dtos;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Builds a Planned shipment's legs from a route plan (POST /Shipments/{id}/route):
//   [first mile]  origin customer address -> origin station        (Courier, FirstMileMinutes)
//   [lanes]       origin station -> ... -> destination station     (one leg per lane, scheduled times)
//   [last mile]   destination station -> destination customer      (Courier, LastMileMinutes)
// First/last-mile legs are only added when an end isn't itself the station. The
// legs replace the existing ones through ApplyLegsAsync, so the usual leg rules apply.
public partial class ShipmentService
{
    public async Task<ShipmentRouteResponse?> RouteAsync(int id, RouteShipmentRequest request)
    {
        var shipment = await ShipmentsWithDetails().FirstOrDefaultAsync(s => s.Id == id);
        if (shipment == null) return null;
        EnsureStatus(shipment, "route", ShipmentStatus.Planned);

        var pickupAt = request.ReadyAt?.UtcDateTime ?? shipment.PlannedPickupAt ?? DateTime.UtcNow;
        var (legs, plan) = await PlanLegsAsync(shipment.OriginLocation, shipment.DestinationLocation, pickupAt, request);

        await ApplyLegsAsync(shipment, legs, shipment.OriginLocationId, shipment.DestinationLocationId);
        shipment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new ShipmentRouteResponse
        {
            Shipment = (await GetShipmentByIdAsync(id))!,
            Plan = plan
        };
    }

    // Plans origin -> destination and turns the plan into leg requests (first mile,
    // one per lane, last mile) without changing anything; the caller applies them.
    private async Task<(List<ShipmentLegRequest> Legs, RoutePlanResponse Plan)> PlanLegsAsync(
        Location origin, Location destination, DateTime pickupAt, RouteShipmentRequest options)
    {
        var hasFirstMile = origin.Type == LocationType.CustomerAddress;
        var stationReadyAt = hasFirstMile ? pickupAt.AddMinutes(options.FirstMileMinutes) : pickupAt;

        var plan = await _routePlanner.PlanAsync(new RoutePlanRequest
        {
            Origin = new RouteEndpointRequest { LocationId = origin.Id },
            Destination = new RouteEndpointRequest { LocationId = destination.Id },
            ReadyAt = new DateTimeOffset(stationReadyAt, TimeSpan.Zero),
            Objective = options.Objective,
            LaneIds = options.LaneIds,
            MaxAlternatives = options.LaneIds is { Count: > 0 } ? 0 : 2
        });

        var legs = new List<ShipmentLegRequest>();
        if (plan.Origin.Station.Id != origin.Id)
        {
            legs.Add(CourierLeg(origin.Id, plan.Origin.Station.Id, pickupAt, stationReadyAt));
        }
        legs.AddRange(plan.Best.Legs.Select(leg => new ShipmentLegRequest
        {
            Mode = leg.Mode,
            OriginLocationId = leg.Origin.Id,
            DestinationLocationId = leg.Destination.Id,
            CarrierId = leg.Carrier?.Id,
            PlannedDeparture = Utc(leg.DepartureAt),
            PlannedArrival = Utc(leg.ArrivalAt)
        }));
        if (plan.Destination.Station.Id != destination.Id)
        {
            var arrival = plan.Best.ArrivalAt;
            legs.Add(CourierLeg(plan.Destination.Station.Id, destination.Id,
                arrival, arrival.AddMinutes(options.LastMileMinutes)));
        }
        return (legs, plan);
    }

    private static ShipmentLegRequest CourierLeg(int originId, int destinationId, DateTime departure, DateTime arrival) => new()
    {
        Mode = TransportMode.Courier,
        OriginLocationId = originId,
        DestinationLocationId = destinationId,
        PlannedDeparture = Utc(departure),
        PlannedArrival = Utc(arrival)
    };

    private static DateTimeOffset Utc(DateTime utc) => new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
