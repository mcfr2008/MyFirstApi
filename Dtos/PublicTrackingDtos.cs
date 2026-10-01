using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

// What anyone with the tracking number may see (no login). Leaves out parties,
// references, notes, who recorded what, GPS, vehicles and proof-of-delivery
// details, and hides the name of customer-address locations.
public class PublicTrackingResponse
{
    public string TrackingNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public CustomsStatus CustomsStatus { get; set; }
    public PublicLocationResponse Origin { get; set; } = null!;
    public PublicLocationResponse Destination { get; set; } = null!;
    public DateTime? PlannedPickupAt { get; set; }
    // ETA of the last leg.
    public DateTime? EstimatedDeliveryAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    // Delivery was confirmed with a receiver signature.
    public bool SignedForDelivery { get; set; }
    public int TotalPieces { get; set; }
    public List<PublicLegResponse> Legs { get; set; } = new();
    // Newest first. Events recorded for several pieces at once are one entry.
    public List<PublicTrackingEventResponse> Events { get; set; } = new();
}

public class PublicLocationResponse
{
    // Null for customer addresses.
    public string? Name { get; set; }
    public LocationType Type { get; set; }
    public string? Province { get; set; }
    public string Country { get; set; } = string.Empty;
    // IANA time zone, for showing local times.
    public string TimeZone { get; set; } = string.Empty;

    public static PublicLocationResponse From(Location location) => new()
    {
        Name = location.Type == LocationType.CustomerAddress ? null : location.Name,
        Type = location.Type,
        Province = location.Province,
        Country = location.Country,
        TimeZone = location.TimeZone
    };
}

public class PublicLegResponse
{
    public int Sequence { get; set; }
    public TransportMode Mode { get; set; }
    public LegStatus Status { get; set; }
    public string? CarrierName { get; set; }
    public PublicLocationResponse Origin { get; set; } = null!;
    public PublicLocationResponse Destination { get; set; } = null!;
    public DateTime? PlannedDeparture { get; set; }
    public DateTime? PlannedArrival { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateTime? ActualArrival { get; set; }

    public static PublicLegResponse From(ShipmentLeg leg) => new()
    {
        Sequence = leg.Sequence,
        Mode = leg.Mode,
        Status = leg.ActualArrival.HasValue ? LegStatus.Arrived
            : leg.ActualDeparture.HasValue ? LegStatus.Departed
            : LegStatus.Planned,
        CarrierName = leg.Carrier?.Name,
        Origin = PublicLocationResponse.From(leg.OriginLocation),
        Destination = PublicLocationResponse.From(leg.DestinationLocation),
        PlannedDeparture = leg.PlannedDeparture,
        PlannedArrival = leg.PlannedArrival,
        ActualDeparture = leg.ActualDeparture,
        ActualArrival = leg.ActualArrival
    };
}

public class PublicTrackingEventResponse
{
    public DateTime OccurredAt { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameTh { get; set; } = string.Empty;
    public PublicLocationResponse? Location { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonNameEn { get; set; }
    public string? ReasonNameTh { get; set; }
    // How many of the shipment's pieces this event covers.
    public int Pieces { get; set; }
}
