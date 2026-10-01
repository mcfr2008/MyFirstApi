using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

// Plans a route through the lane network (read-only; nothing is saved):
// 1. Each end is resolved to a network point: a non-customer location as is, an
//    address or customer location through its service area's station.
// 2. A Dijkstra search over active lanes finds the best path for the objective.
//    Fastest is time-dependent: at each point it takes the lane's next scheduled
//    departure (DepartureTimes / OperatingDays in the origin's time zone), so waits
//    for the next truck/vessel are part of the ETA.
// 3. Alternatives: the search is re-run with each lane of the best path excluded in
//    turn (a simplified Yen's k-shortest-paths), keeping distinct paths.
public class RoutePlannerService : IRoutePlannerService
{
    private const int MaxLegs = 12;

    private readonly AppDbContext _context;
    private readonly IServiceAreaService _serviceAreas;

    public RoutePlannerService(AppDbContext context, IServiceAreaService serviceAreas)
    {
        _context = context;
        _serviceAreas = serviceAreas;
    }

    public async Task<RoutePlanResponse> PlanAsync(RoutePlanRequest request)
    {
        var readyAt = request.ReadyAt?.UtcDateTime ?? DateTime.UtcNow;
        var origin = await ResolveEndpointAsync(request.Origin!, "origin");
        var destination = await ResolveEndpointAsync(request.Destination!, "destination");

        var lanes = await _context.Lanes.AsNoTracking()
            .Include(l => l.OriginLocation)
            .Include(l => l.DestinationLocation)
            .Include(l => l.Carrier)
            .Where(l => l.IsActive)
            .ToListAsync();
        var factors = await _context.EmissionFactors.AsNoTracking().Where(f => f.IsActive).ToListAsync();
        var network = lanes.Select(l => new NetworkLane(l, factors)).ToLookup(l => l.Lane.OriginLocationId);

        var start = origin.Station.Id;
        var target = destination.Station.Id;
        var bestPath = Search(network, start, target, readyAt, request.Objective, new HashSet<int>())
            ?? throw Errors.RouteNotFound(origin.Station.Code, destination.Station.Code, request.Objective);
        var best = BuildOption(bestPath, readyAt, request.WeightKg)!;

        var alternatives = new List<RouteOptionResponse>();
        var seen = new HashSet<string> { Signature(bestPath) };
        foreach (var lane in bestPath)
        {
            var path = Search(network, start, target, readyAt, request.Objective, [lane.Lane.Id]);
            if (path == null || !seen.Add(Signature(path))) continue;

            var option = BuildOption(path, readyAt, request.WeightKg);
            if (option != null) alternatives.Add(option);
        }

        return new RoutePlanResponse
        {
            Objective = request.Objective,
            ReadyAt = readyAt,
            WeightKg = request.WeightKg,
            Origin = origin,
            Destination = destination,
            Best = best,
            Alternatives = Rank(alternatives, request.Objective).Take(request.MaxAlternatives).ToList()
        };
    }

    private async Task<RouteEndpointResponse> ResolveEndpointAsync(RouteEndpointRequest endpoint, string end)
    {
        Location? location = null;
        string country = endpoint.Country, postalCode = endpoint.PostalCode ?? "", province = endpoint.Province ?? "";
        if (endpoint.LocationId.HasValue)
        {
            location = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == endpoint.LocationId.Value)
                ?? throw Errors.ReferenceNotFound($"{end}.locationId", endpoint.LocationId.Value);
            if (location.Type != LocationType.CustomerAddress)
            {
                // A network point: the route starts/ends right there.
                var point = RoutePoint.From(location);
                return new RouteEndpointResponse { Location = point, Station = point };
            }
            country = location.Country;
            postalCode = location.PostalCode ?? "";
            province = location.Province ?? "";
        }

        var match = await _serviceAreas.FindAsync(country, postalCode, province)
            ?? throw Errors.RouteEndpointNotCovered(end, ServiceAreaService.Coverage(
                country.Trim().ToUpperInvariant(), QueryHelpers.NullIfBlank(postalCode), QueryHelpers.NullIfBlank(province)));
        var station = await _context.Locations.AsNoTracking().FirstAsync(l => l.Id == match.ServiceArea.Station.Id);

        return new RouteEndpointResponse
        {
            Location = location == null ? null : RoutePoint.From(location),
            ServiceArea = new ReferenceSummary(match.ServiceArea.Id, match.ServiceArea.Code, match.ServiceArea.Name),
            MatchedBy = match.MatchedBy,
            Station = RoutePoint.From(station)
        };
    }

    // Dijkstra from start to target; returns the lanes in order (empty when start == target),
    // or null when the target can't be reached.
    private static List<NetworkLane>? Search(
        ILookup<int, NetworkLane> network, int start, int target, DateTime readyAt,
        RouteObjective objective, HashSet<int> excludedLaneIds)
    {
        var best = new Dictionary<int, (double Cost, DateTime Time, int Legs)> { [start] = (0, readyAt, 0) };
        var previous = new Dictionary<int, NetworkLane>();
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var node, out var queuedCost))
        {
            var current = best[node];
            if (queuedCost > current.Cost) continue;
            if (node == target) break;
            if (current.Legs >= MaxLegs) continue;

            foreach (var lane in network[node])
            {
                if (excludedLaneIds.Contains(lane.Lane.Id)) continue;

                double cost;
                var time = current.Time;
                if (objective == RouteObjective.Fastest)
                {
                    var departure = lane.NextDeparture(current.Time);
                    if (departure == null) continue;
                    time = departure.Value.AddMinutes(lane.Lane.TransitTimeMinutes);
                    cost = (time - readyAt).TotalMinutes;
                }
                else
                {
                    var weight = objective == RouteObjective.Shortest ? lane.DistanceKm : lane.Co2eKgPerTonne;
                    if (weight == null) continue;
                    cost = current.Cost + (double)weight.Value;
                }

                var next = lane.Lane.DestinationLocationId;
                if (best.TryGetValue(next, out var known) && cost >= known.Cost) continue;

                best[next] = (cost, time, current.Legs + 1);
                previous[next] = lane;
                queue.Enqueue(next, cost);
            }
        }

        if (start == target) return new();
        if (!previous.ContainsKey(target)) return null;

        var path = new List<NetworkLane>();
        for (var node = target; node != start && path.Count <= MaxLegs; node = previous[node].Lane.OriginLocationId)
        {
            path.Add(previous[node]);
        }
        path.Reverse();
        return path;
    }

    // Walks the path from readyAt, taking each lane's next departure. Null if a lane
    // never departs (shouldn't happen: every lane runs at least once a week).
    private static RouteOptionResponse? BuildOption(List<NetworkLane> path, DateTime readyAt, decimal? weightKg)
    {
        var legs = new List<RouteLegResponse>();
        var time = readyAt;
        foreach (var lane in path)
        {
            var departure = lane.NextDeparture(time);
            if (departure == null) return null;

            var arrival = departure.Value.AddMinutes(lane.Lane.TransitTimeMinutes);
            legs.Add(new RouteLegResponse
            {
                Sequence = legs.Count + 1,
                Lane = new ReferenceSummary(lane.Lane.Id, lane.Lane.Code, lane.Lane.Name),
                Mode = lane.Lane.Mode,
                Carrier = lane.Lane.Carrier == null
                    ? null
                    : new ReferenceSummary(lane.Lane.Carrier.Id, lane.Lane.Carrier.Code, lane.Lane.Carrier.Name),
                Origin = RoutePoint.From(lane.Lane.OriginLocation),
                Destination = RoutePoint.From(lane.Lane.DestinationLocation),
                WaitMinutes = Minutes(departure.Value - time),
                DepartureAt = departure.Value,
                ArrivalAt = arrival,
                TransitMinutes = lane.Lane.TransitTimeMinutes,
                DistanceKm = lane.DistanceKm.HasValue ? Math.Round(lane.DistanceKm.Value, 1) : null,
                DistanceSource = lane.DistanceSource,
                Co2eKgPerTonne = lane.Co2eKgPerTonne.HasValue ? Math.Round(lane.Co2eKgPerTonne.Value, 3) : null
            });
            time = arrival;
        }

        var distance = legs.All(l => l.DistanceKm.HasValue) ? legs.Sum(l => l.DistanceKm!.Value) : (decimal?)null;
        var perTonne = legs.All(l => l.Co2eKgPerTonne.HasValue) ? legs.Sum(l => l.Co2eKgPerTonne!.Value) : (decimal?)null;
        return new RouteOptionResponse
        {
            DepartureAt = legs.Count > 0 ? legs[0].DepartureAt : readyAt,
            ArrivalAt = time,
            TotalMinutes = Minutes(time - readyAt),
            TransitMinutes = legs.Sum(l => l.TransitMinutes),
            WaitMinutes = legs.Sum(l => l.WaitMinutes),
            DistanceKm = distance,
            Co2eKgPerTonne = perTonne,
            Co2eKg = perTonne.HasValue && weightKg.HasValue ? Math.Round(perTonne.Value * weightKg.Value / 1000m, 3) : null,
            Legs = legs
        };
    }

    private static IEnumerable<RouteOptionResponse> Rank(IEnumerable<RouteOptionResponse> options, RouteObjective objective) =>
        objective switch
        {
            RouteObjective.Shortest => options.OrderBy(o => o.DistanceKm ?? decimal.MaxValue).ThenBy(o => o.ArrivalAt),
            RouteObjective.LowestEmissions => options.OrderBy(o => o.Co2eKgPerTonne ?? decimal.MaxValue).ThenBy(o => o.ArrivalAt),
            _ => options.OrderBy(o => o.ArrivalAt).ThenBy(o => o.DistanceKm ?? decimal.MaxValue)
        };

    private static string Signature(List<NetworkLane> path) => string.Join(",", path.Select(l => l.Lane.Id));

    private static int Minutes(TimeSpan span) => (int)Math.Round(span.TotalMinutes);

    // A lane with what the search needs precomputed: distance, CO2e per tonne and its schedule.
    private sealed class NetworkLane
    {
        private readonly TimeZoneInfo _timeZone;
        private readonly HashSet<DayOfWeek> _days;
        private readonly List<TimeSpan> _times;

        public NetworkLane(Lane lane, List<EmissionFactor> factors)
        {
            Lane = lane;
            if (lane.DistanceKm.HasValue)
            {
                DistanceKm = lane.DistanceKm;
                DistanceSource = DistanceSource.Lane;
            }
            else
            {
                DistanceKm = TransportEstimates.EstimateKm(lane.Mode, lane.OriginLocation, lane.DestinationLocation);
                DistanceSource = DistanceKm.HasValue ? DistanceSource.GreatCircle : DistanceSource.Unknown;
            }

            var factor = TransportEstimates.PickFactor(factors, lane.Mode, lane.CarrierId);
            Co2eKgPerTonne = DistanceKm.HasValue && factor != null
                ? DistanceKm.Value * factor.GramsCo2ePerTonneKm / 1000m
                : null;

            _timeZone = TimeZoneInfo.TryFindSystemTimeZoneById(lane.OriginLocation.TimeZone, out var timeZone)
                ? timeZone
                : TimeZoneInfo.Utc;
            _days = lane.OperatingDays.Select(Enum.Parse<DayOfWeek>).ToHashSet();
            _times = lane.DepartureTimes.Select(t => TimeSpan.ParseExact(t, @"hh\:mm", null)).Order().ToList();
        }

        public Lane Lane { get; }
        public decimal? DistanceKm { get; }
        public DistanceSource DistanceSource { get; }
        public decimal? Co2eKgPerTonne { get; }

        // First departure at or after utcTime, in UTC; scheduled times are local to
        // the lane's origin. No departure times = leaves as soon as the goods are there.
        public DateTime? NextDeparture(DateTime utcTime)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(utcTime, _timeZone);
            for (var day = 0; day <= 7; day++)
            {
                var date = local.Date.AddDays(day);
                if (_days.Count > 0 && !_days.Contains(date.DayOfWeek)) continue;

                if (_times.Count == 0)
                {
                    return day == 0 ? utcTime : ToUtc(date);
                }
                foreach (var time in _times)
                {
                    var candidate = date + time;
                    if (candidate >= local) return ToUtc(candidate);
                }
            }
            return null;
        }

        private DateTime ToUtc(DateTime local)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            // A time skipped by a daylight-saving jump moves to the next valid hour.
            if (_timeZone.IsInvalidTime(local)) local = local.AddHours(1);
            return TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
        }
    }
}
