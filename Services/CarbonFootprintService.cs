using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Shipment emissions following ISO 14083 / the GLEC Framework (well-to-wheel):
// for each leg, CO2e = shipment mass (t) x leg distance (km) x emission factor (gCO2e/tkm).
// - Mass: sum of WeightKg of the items currently in the shipment.
// - Distance: an active lane between the same two points (same mode family), else the
//   great-circle distance between their coordinates times a distance adjustment factor.
// - Factor: the leg carrier's own factor for the mode, else the mode's default.
// Calculated on demand from current data; legs that lack data are reported, not guessed.
public class CarbonFootprintService : ICarbonFootprintService
{
    public const string Methodology = "ISO 14083:2023 / GLEC Framework, well-to-wheel (WTW) CO2e";

    // Great-circle distances are shorter than real routes. Sea and air follow the
    // GLEC Framework (x1.15, +95 km); road and rail use a typical x1.2 detour factor.
    private static decimal AdjustGreatCircle(TransportMode mode, decimal km) => mode switch
    {
        TransportMode.Sea => km * 1.15m,
        TransportMode.Air => km + 95m,
        _ => km * 1.2m
    };

    private readonly AppDbContext _context;

    public CarbonFootprintService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ShipmentEmissionsResponse?> CalculateAsync(int shipmentId)
    {
        var shipment = await _context.Shipments.AsNoTracking()
            .Include(s => s.Legs).ThenInclude(l => l.OriginLocation)
            .Include(s => s.Legs).ThenInclude(l => l.DestinationLocation)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == shipmentId);
        if (shipment == null) return null;

        var weights = await _context.ShipmentItems
            .Where(si => si.ShipmentId == shipmentId)
            .Select(si => si.TrackedItem.WeightKg)
            .ToListAsync();
        var massKg = weights.Sum(w => w ?? 0);
        var tonnes = massKg / 1000m;

        var legs = shipment.Legs.OrderBy(l => l.Sequence).ToList();
        var pointIds = legs.SelectMany(l => new[] { l.OriginLocationId, l.DestinationLocationId }).Distinct().ToList();
        var lanes = await _context.Lanes.AsNoTracking()
            .Where(l => l.IsActive && l.DistanceKm != null &&
                        pointIds.Contains(l.OriginLocationId) && pointIds.Contains(l.DestinationLocationId))
            .ToListAsync();
        var factors = await _context.EmissionFactors.AsNoTracking()
            .Where(f => f.IsActive)
            .ToListAsync();

        var gaps = new HashSet<EmissionDataGap>();
        if (weights.Count == 0) gaps.Add(EmissionDataGap.NoItems);
        if (weights.Any(w => w == null)) gaps.Add(EmissionDataGap.ItemsWithoutWeight);

        var legResults = new List<LegEmissionsResponse>();
        foreach (var leg in legs)
        {
            var (distance, source) = ResolveDistance(leg, lanes);
            var factor = factors.FirstOrDefault(f => f.Mode == leg.Mode && leg.CarrierId != null && f.CarrierId == leg.CarrierId)
                         ?? factors.FirstOrDefault(f => f.Mode == leg.Mode && f.CarrierId == null);

            if (distance == null) gaps.Add(EmissionDataGap.LegDistanceUnknown);
            if (factor == null) gaps.Add(EmissionDataGap.EmissionFactorMissing);

            var tonneKm = distance.HasValue ? Math.Round(tonnes * distance.Value, 3) : (decimal?)null;
            legResults.Add(new LegEmissionsResponse
            {
                Sequence = leg.Sequence,
                Mode = leg.Mode,
                Origin = new ReferenceSummary(leg.OriginLocation.Id, leg.OriginLocation.Code, leg.OriginLocation.Name),
                Destination = new ReferenceSummary(leg.DestinationLocation.Id, leg.DestinationLocation.Code, leg.DestinationLocation.Name),
                DistanceKm = distance.HasValue ? Math.Round(distance.Value, 1) : null,
                DistanceSource = source,
                TonneKm = tonneKm,
                EmissionFactor = factor == null
                    ? null
                    : new EmissionFactorSummary(factor.Id, factor.Code, factor.GramsCo2ePerTonneKm, factor.Source),
                Co2eKg = tonneKm.HasValue && factor != null
                    ? Math.Round(tonnes * distance!.Value * factor.GramsCo2ePerTonneKm / 1000m, 3)
                    : null
            });
        }

        return new ShipmentEmissionsResponse
        {
            ShipmentId = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            Methodology = Methodology,
            TotalCo2eKg = legResults.Sum(l => l.Co2eKg ?? 0),
            TotalTonneKm = legResults.Sum(l => l.TonneKm ?? 0),
            IsComplete = gaps.Count == 0,
            DataGaps = gaps.Order().ToList(),
            Pieces = weights.Count,
            PiecesWithoutWeight = weights.Count(w => w == null),
            MassKg = massKg,
            Legs = legResults
        };
    }

    private static (decimal? Km, DistanceSource Source) ResolveDistance(ShipmentLeg leg, List<Lane> lanes)
    {
        var lane = lanes
            .Where(l => l.OriginLocationId == leg.OriginLocationId &&
                        l.DestinationLocationId == leg.DestinationLocationId &&
                        TransportModes.SameFamily(l.Mode, leg.Mode))
            .OrderBy(l => l.Mode == leg.Mode ? 0 : 1)
            .FirstOrDefault();
        if (lane != null) return (lane.DistanceKm, DistanceSource.Lane);

        var from = leg.OriginLocation;
        var to = leg.DestinationLocation;
        if (from.Latitude is { } lat1 && from.Longitude is { } lon1 &&
            to.Latitude is { } lat2 && to.Longitude is { } lon2)
        {
            var km = GreatCircleKm((double)lat1, (double)lon1, (double)lat2, (double)lon2);
            return (AdjustGreatCircle(leg.Mode, (decimal)km), DistanceSource.GreatCircle);
        }
        return (null, DistanceSource.Unknown);
    }

    // Haversine distance on a sphere of mean Earth radius.
    private static double GreatCircleKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        static double Rad(double degrees) => degrees * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(a));
    }
}
