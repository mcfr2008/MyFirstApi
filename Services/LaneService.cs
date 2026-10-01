using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class LaneService
    : MasterDataService<Lane, LaneRequest, LaneResponse, LaneQuery>, ILaneService
{
    // Network points a lane can connect: everything except a customer's address
    // (the last mile to a customer is a shipment leg, not a scheduled lane).
    private static readonly LocationType[] EndpointTypes = Enum.GetValues<LocationType>()
        .Where(t => t != LocationType.CustomerAddress)
        .ToArray();

    public LaneService(AppDbContext context) : base(context)
    {
    }

    protected override DbSet<Lane> Set => Context.Lanes;

    protected override string EntityName => "Lane";

    protected override Expression<Func<Lane, LaneResponse>> Projection => l => new LaneResponse
    {
        Id = l.Id,
        Code = l.Code,
        Name = l.Name,
        Origin = new ReferenceSummary(l.OriginLocation.Id, l.OriginLocation.Code, l.OriginLocation.Name),
        Destination = new ReferenceSummary(l.DestinationLocation.Id, l.DestinationLocation.Code, l.DestinationLocation.Name),
        Mode = l.Mode,
        Carrier = l.Carrier == null ? null : new ReferenceSummary(l.Carrier.Id, l.Carrier.Code, l.Carrier.Name),
        TransitTimeMinutes = l.TransitTimeMinutes,
        DistanceKm = l.DistanceKm,
        DepartureTimes = l.DepartureTimes,
        OperatingDayNames = l.OperatingDays,
        IsActive = l.IsActive,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt
    };

    protected override Expression<Func<Lane, bool>> MatchesSearch(string pattern) =>
        l => EF.Functions.ILike(l.Code, pattern) ||
             EF.Functions.ILike(l.Name, pattern);

    protected override IQueryable<Lane> ApplyFilters(IQueryable<Lane> items, LaneQuery query)
    {
        if (query.OriginLocationId.HasValue)
        {
            items = items.Where(l => l.OriginLocationId == query.OriginLocationId);
        }
        if (query.DestinationLocationId.HasValue)
        {
            items = items.Where(l => l.DestinationLocationId == query.DestinationLocationId);
        }
        if (query.LocationId.HasValue)
        {
            items = items.Where(l => l.OriginLocationId == query.LocationId || l.DestinationLocationId == query.LocationId);
        }
        if (query.Mode.HasValue)
        {
            items = items.Where(l => l.Mode == query.Mode.Value);
        }
        if (query.CarrierId.HasValue)
        {
            items = items.Where(l => l.CarrierId == query.CarrierId);
        }
        return items;
    }

    protected override IOrderedQueryable<Lane> ApplyOrder(IQueryable<Lane> items) =>
        items.OrderBy(l => l.OriginLocation.Code).ThenBy(l => l.DestinationLocation.Code).ThenBy(l => l.Code);

    protected override async Task ValidateAsync(Lane entity, LaneRequest request)
    {
        var isNew = entity.Id == 0;

        var origin = await ReferenceResolver.ResolveRequiredAsync(
            Context.Locations.AsNoTracking(), request.OriginLocationId!.Value,
            isNew ? null : entity.OriginLocationId, "originLocationId");
        EnsureEndpoint(origin, "originLocationId");

        var destination = await ReferenceResolver.ResolveRequiredAsync(
            Context.Locations.AsNoTracking(), request.DestinationLocationId!.Value,
            isNew ? null : entity.DestinationLocationId, "destinationLocationId");
        EnsureEndpoint(destination, "destinationLocationId");

        var carrier = await ReferenceResolver.ResolveAsync(
            Context.Carriers.AsNoTracking(), request.CarrierId, entity.CarrierId, "carrierId");
        var mode = request.Mode!.Value;
        if (carrier != null && !carrier.Modes.Any(m => TransportModes.SameFamily(Enum.Parse<TransportMode>(m), mode)))
        {
            throw Errors.CarrierModeNotSupported(carrier.Code, mode);
        }
    }

    protected override void Apply(Lane entity, LaneRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.OriginLocationId = request.OriginLocationId!.Value;
        entity.DestinationLocationId = request.DestinationLocationId!.Value;
        entity.Mode = request.Mode!.Value;
        entity.CarrierId = request.CarrierId;
        entity.TransitTimeMinutes = request.TransitTimeMinutes!.Value;
        entity.DistanceKm = request.DistanceKm;
        entity.DepartureTimes = request.DepartureTimes.Select(t => t.Trim()).Distinct().Order().ToList();
        entity.OperatingDays = request.OperatingDays.Distinct().Order().Select(d => d.ToString()).ToList();
    }

    private static void EnsureEndpoint(Location location, string field)
    {
        if (!EndpointTypes.Contains(location.Type))
        {
            throw Errors.LocationTypeNotAllowed(field, location.Code, location.Type, EndpointTypes.Cast<object>());
        }
    }
}
