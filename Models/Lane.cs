namespace MyFirstApi.Models;

// A scheduled transport connection between two network points (station -> hub,
// hub -> hub linehaul, hub -> station, port -> port, ...). Lanes are one-way:
// the return trip is its own lane. The route planner chains lanes together.
public class Lane : IMasterData
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int OriginLocationId { get; set; }
    public Location OriginLocation { get; set; } = null!;
    public int DestinationLocationId { get; set; }
    public Location DestinationLocation { get; set; } = null!;
    public TransportMode Mode { get; set; }
    public int? CarrierId { get; set; }
    public Carrier? Carrier { get; set; }
    // Door-to-door time from departure at the origin to arrival at the destination.
    public int TransitTimeMinutes { get; set; }
    public decimal? DistanceKm { get; set; }
    // Scheduled departures as "HH:mm" in the origin's time zone; empty = any time (on demand).
    public List<string> DepartureTimes { get; set; } = new();
    // Weekday names (Weekday enum); empty = every day.
    public List<string> OperatingDays { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
