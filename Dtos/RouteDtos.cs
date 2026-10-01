using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter<RouteObjective>))]
public enum RouteObjective
{
    // Earliest arrival, counting waits for scheduled departures.
    Fastest,
    // Fewest kilometres.
    Shortest,
    // Least CO2e per tonne (distance x emission factor of each lane).
    LowestEmissions
}

// Where a route starts or ends: an existing location, or an address (postal code /
// province) that is looked up in the service areas.
public class RouteEndpointRequest : IValidatableObject
{
    // A network point (hub, branch, port, ...) is used as is; a CustomerAddress is
    // mapped to its station through the service areas.
    public int? LocationId { get; set; }

    [RegularExpression(IsoRules.CountryPattern, ErrorMessage = IsoRules.CountryMessage)]
    public string Country { get; set; } = "TH";

    [StringLength(100)]
    public string? Province { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (LocationId == null && string.IsNullOrWhiteSpace(PostalCode) && string.IsNullOrWhiteSpace(Province))
        {
            yield return new ValidationResult("Provide locationId, postalCode or province.");
        }
    }
}

public class RoutePlanRequest
{
    [Required]
    public RouteEndpointRequest? Origin { get; set; }

    [Required]
    public RouteEndpointRequest? Destination { get; set; }

    // When the goods are ready at the origin station. Defaults to now.
    public DateTimeOffset? ReadyAt { get; set; }

    public RouteObjective Objective { get; set; } = RouteObjective.Fastest;

    // Shipment weight, to give CO2e in kg (always given per tonne as well).
    [Range(typeof(decimal), "0.001", "100000000")]
    public decimal? WeightKg { get; set; }

    // Other routes to suggest besides the best one.
    [Range(0, 5)]
    public int MaxAlternatives { get; set; } = 2;
}

// A location on a route, with its time zone for showing local times.
public record RoutePoint(int Id, string Code, string Name, LocationType Type, string TimeZone)
{
    public static RoutePoint From(Location location) =>
        new(location.Id, location.Code, location.Name, location.Type, location.TimeZone);
}

public class RouteEndpointResponse
{
    // The location from the request, if one was given.
    public RoutePoint? Location { get; set; }
    // The service area used to find the station (address or customer location).
    public ReferenceSummary? ServiceArea { get; set; }
    public ServiceAreaMatch? MatchedBy { get; set; }
    // Where the route starts or ends in the network.
    public RoutePoint Station { get; set; } = null!;
}

public class RoutePlanResponse
{
    public RouteObjective Objective { get; set; }
    public DateTime ReadyAt { get; set; }
    public decimal? WeightKg { get; set; }
    public RouteEndpointResponse Origin { get; set; } = null!;
    public RouteEndpointResponse Destination { get; set; } = null!;
    public RouteOptionResponse Best { get; set; } = null!;
    // Ordered by the objective, best first.
    public List<RouteOptionResponse> Alternatives { get; set; } = new();
}

public class RouteOptionResponse
{
    // First scheduled departure, and ETA at the destination station.
    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }
    // From ReadyAt to ArrivalAt = TransitMinutes + WaitMinutes.
    public int TotalMinutes { get; set; }
    public int TransitMinutes { get; set; }
    public int WaitMinutes { get; set; }
    // Null when a lane's distance can't be determined (see each leg).
    public decimal? DistanceKm { get; set; }
    public decimal? Co2eKgPerTonne { get; set; }
    // Co2eKgPerTonne x weight, when WeightKg was given.
    public decimal? Co2eKg { get; set; }
    public List<RouteLegResponse> Legs { get; set; } = new();
}

public class RouteLegResponse
{
    public int Sequence { get; set; }
    public ReferenceSummary Lane { get; set; } = null!;
    public TransportMode Mode { get; set; }
    public ReferenceSummary? Carrier { get; set; }
    public RoutePoint Origin { get; set; } = null!;
    public RoutePoint Destination { get; set; } = null!;
    // Waiting at the origin for this departure.
    public int WaitMinutes { get; set; }
    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }
    public int TransitMinutes { get; set; }
    public decimal? DistanceKm { get; set; }
    public DistanceSource DistanceSource { get; set; }
    public decimal? Co2eKgPerTonne { get; set; }
}
