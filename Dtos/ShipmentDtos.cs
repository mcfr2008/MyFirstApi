using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

public static class Incoterms
{
    // Incoterms 2020.
    public static readonly HashSet<string> All =
        ["EXW", "FCA", "FAS", "FOB", "CFR", "CIF", "CPT", "CIP", "DAP", "DPU", "DDP"];
}

public class ShipmentLegRequest : IValidatableObject
{
    [Required]
    public TransportMode? Mode { get; set; }

    [Required]
    public int? OriginLocationId { get; set; }

    [Required]
    public int? DestinationLocationId { get; set; }

    public int? CarrierId { get; set; }

    // A vehicle from /api/v1/Vehicles, or VehicleName as text for outside carriers.
    public int? VehicleId { get; set; }

    [StringLength(255)]
    public string? VehicleName { get; set; }

    // Voyage (sea), flight (air), train (rail) or trip (road) number.
    [StringLength(50)]
    public string? VoyageNumber { get; set; }

    // ETD / ETA
    public DateTimeOffset? PlannedDeparture { get; set; }
    public DateTimeOffset? PlannedArrival { get; set; }

    // Defaults from the mode (Sea -> BillOfLading, Air -> AirWaybill, ...) when a number is given.
    public TransportDocumentType? DocumentType { get; set; }

    [StringLength(100)]
    public string? DocumentNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OriginLocationId.HasValue && OriginLocationId == DestinationLocationId)
        {
            yield return new ValidationResult(
                "A leg's origin and destination must differ.", [nameof(DestinationLocationId)]);
        }
        if (PlannedDeparture.HasValue && PlannedArrival.HasValue && PlannedArrival < PlannedDeparture)
        {
            yield return new ValidationResult(
                "PlannedArrival must be after PlannedDeparture.", [nameof(PlannedArrival)]);
        }
    }
}

public class ShipmentFields : IValidatableObject
{
    [StringLength(100)]
    public string? Reference { get; set; }

    [Required]
    public int? SenderPartyId { get; set; }

    [Required]
    public int? ReceiverPartyId { get; set; }

    [Required]
    public int? OriginLocationId { get; set; }

    [Required]
    public int? DestinationLocationId { get; set; }

    // Incoterms 2020 rule, e.g. FOB, CIF, DAP.
    public string? Incoterm { get; set; }

    // Defaults to Pending for international shipments, NotRequired for domestic.
    public CustomsStatus? CustomsStatus { get; set; }

    public DateTimeOffset? PlannedPickupAt { get; set; }

    // Delivery must be confirmed with a receiver signature (proof of delivery). Default true.
    public bool? RequiresSignature { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    // In order: legs[0] starts at the origin, each leg starts where the previous
    // one ends, and the last leg ends at the destination.
    [MaxLength(20)]
    public List<ShipmentLegRequest> Legs { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OriginLocationId.HasValue && OriginLocationId == DestinationLocationId)
        {
            yield return new ValidationResult(
                "Origin and destination must differ.", [nameof(DestinationLocationId)]);
        }
        if (!string.IsNullOrWhiteSpace(Incoterm) && !Incoterms.All.Contains(Incoterm.Trim().ToUpperInvariant()))
        {
            yield return new ValidationResult(
                $"Incoterm must be one of: {string.Join(", ", Incoterms.All)}.", [nameof(Incoterm)]);
        }
    }
}

public class CreateShipmentRequest : ShipmentFields
{
    // Optional: generated (TS + date + random) when omitted.
    [RegularExpression("^[A-Za-z0-9-]{6,64}$", ErrorMessage = "TrackingNumber must be 6-64 letters, digits or '-'.")]
    public string? TrackingNumber { get; set; }

    // Items to include right away (containers add everything inside them).
    public List<int>? TrackedItemIds { get; set; }
    public List<string>? TagCodes { get; set; }
    public List<int>? ContainerIds { get; set; }
}

public class UpdateShipmentRequest : ShipmentFields
{
}

public class ShipmentItemsRequest : IValidatableObject
{
    public List<int>? TrackedItemIds { get; set; }
    public List<string>? TagCodes { get; set; }
    public List<int>? ContainerIds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((TrackedItemIds?.Count ?? 0) + (TagCodes?.Count ?? 0) + (ContainerIds?.Count ?? 0) == 0)
        {
            yield return new ValidationResult("Provide TrackedItemIds, TagCodes or ContainerIds.");
        }
    }
}

// Changes allowed on a leg that hasn't departed yet (route changes need PUT on the shipment).
public class UpdateShipmentLegRequest : IValidatableObject
{
    public int? CarrierId { get; set; }
    public int? VehicleId { get; set; }

    [StringLength(255)]
    public string? VehicleName { get; set; }

    [StringLength(50)]
    public string? VoyageNumber { get; set; }

    public DateTimeOffset? PlannedDeparture { get; set; }
    public DateTimeOffset? PlannedArrival { get; set; }
    public TransportDocumentType? DocumentType { get; set; }

    [StringLength(100)]
    public string? DocumentNumber { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PlannedDeparture.HasValue && PlannedArrival.HasValue && PlannedArrival < PlannedDeparture)
        {
            yield return new ValidationResult(
                "PlannedArrival must be after PlannedDeparture.", [nameof(PlannedArrival)]);
        }
    }
}

// Depart / arrive a leg; OccurredAt becomes ATD / ATA.
public class LegMovementRequest
{
    public DateTimeOffset? OccurredAt { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}

public class CustomsUpdateRequest
{
    [Required]
    public CustomsStatus? Status { get; set; }

    // Where customs is handling it (e.g. the port); defaults to the current leg's location.
    public int? LocationId { get; set; }

    // Required for Hold (CUSTOMS_HOLD needs a reason), e.g. MISSING_DOCUMENTS.
    [StringLength(50)]
    public string? ReasonCode { get; set; }

    public DateTimeOffset? OccurredAt { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}

public class DeliverShipmentRequest
{
    public DateTimeOffset? OccurredAt { get; set; }

    // Name of the person who signed for it.
    [StringLength(255)]
    public string? ReceivedBy { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}

public class ShipmentQuery : PagedQuery
{
    // Tracking number, reference or any leg's document / voyage number.
    public string? Search { get; set; }
    public ShipmentStatus? Status { get; set; }
    public CustomsStatus? CustomsStatus { get; set; }
    public int? SenderPartyId { get; set; }
    public int? ReceiverPartyId { get; set; }
    public int? OriginLocationId { get; set; }
    public int? DestinationLocationId { get; set; }
    // Shipments with at least one leg of this mode.
    public TransportMode? Mode { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<LegStatus>))]
public enum LegStatus
{
    Planned,
    Departed,
    Arrived
}

public class ShipmentLegResponse
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public TransportMode Mode { get; set; }
    public LegStatus Status { get; set; }
    public ReferenceSummary? Carrier { get; set; }
    public ReferenceSummary? Vehicle { get; set; }
    public string? VehicleName { get; set; }
    public string? VoyageNumber { get; set; }
    public ReferenceSummary Origin { get; set; } = null!;
    public ReferenceSummary Destination { get; set; } = null!;
    public DateTime? PlannedDeparture { get; set; }
    public DateTime? PlannedArrival { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateTime? ActualArrival { get; set; }
    // Positive = late vs. ETA (actual arrival, or now if not arrived yet and ETA has passed).
    public int? ArrivalDelayMinutes { get; set; }
    public TransportDocumentType? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    // Carrier's tracking page for this document, if the carrier has a URL template.
    public string? CarrierTrackingUrl { get; set; }

    public static ShipmentLegResponse From(ShipmentLeg leg, DateTime now)
    {
        var status = leg.ActualArrival.HasValue ? LegStatus.Arrived
            : leg.ActualDeparture.HasValue ? LegStatus.Departed
            : LegStatus.Planned;

        int? delay = null;
        if (leg.PlannedArrival.HasValue)
        {
            var reference = leg.ActualArrival ?? (now > leg.PlannedArrival.Value ? now : (DateTime?)null);
            if (reference.HasValue)
            {
                delay = (int)Math.Round((reference.Value - leg.PlannedArrival.Value).TotalMinutes);
            }
        }

        var trackingNumber = leg.DocumentNumber ?? leg.VoyageNumber;
        return new ShipmentLegResponse
        {
            Id = leg.Id,
            Sequence = leg.Sequence,
            Mode = leg.Mode,
            Status = status,
            Carrier = leg.Carrier == null ? null : new ReferenceSummary(leg.Carrier.Id, leg.Carrier.Code, leg.Carrier.Name),
            Vehicle = leg.Vehicle == null ? null : new ReferenceSummary(leg.Vehicle.Id, leg.Vehicle.Code, leg.Vehicle.Name),
            VehicleName = leg.VehicleName,
            VoyageNumber = leg.VoyageNumber,
            Origin = new ReferenceSummary(leg.OriginLocation.Id, leg.OriginLocation.Code, leg.OriginLocation.Name),
            Destination = new ReferenceSummary(leg.DestinationLocation.Id, leg.DestinationLocation.Code, leg.DestinationLocation.Name),
            PlannedDeparture = leg.PlannedDeparture,
            PlannedArrival = leg.PlannedArrival,
            ActualDeparture = leg.ActualDeparture,
            ActualArrival = leg.ActualArrival,
            ArrivalDelayMinutes = delay,
            DocumentType = leg.DocumentType,
            DocumentNumber = leg.DocumentNumber,
            CarrierTrackingUrl = leg.Carrier?.TrackingUrlTemplate == null || trackingNumber == null
                ? null
                : leg.Carrier.TrackingUrlTemplate.Replace("{number}", Uri.EscapeDataString(trackingNumber))
        };
    }
}

public class ShipmentResponse
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public ReferenceSummary Sender { get; set; } = null!;
    public ReferenceSummary Receiver { get; set; } = null!;
    public ReferenceSummary Origin { get; set; } = null!;
    public ReferenceSummary Destination { get; set; } = null!;
    public ShipmentStatus Status { get; set; }
    // Origin and destination are in different countries.
    public bool IsInternational { get; set; }
    public string? Incoterm { get; set; }
    public CustomsStatus CustomsStatus { get; set; }
    public DateTime? PlannedPickupAt { get; set; }
    public bool RequiresSignature { get; set; }
    // True once a proof of delivery (signature) exists.
    public bool HasProofOfDelivery { get; set; }
    // ETA of the last leg.
    public DateTime? EstimatedArrival { get; set; }
    public DateTime? DeliveredAt { get; set; }
    // Sequence of the leg in progress or next to depart (null when all legs arrived).
    public int? CurrentLegSequence { get; set; }
    public int ItemCount { get; set; }
    public string? Notes { get; set; }
    public List<ShipmentLegResponse> Legs { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public static ShipmentResponse From(Shipment shipment, int itemCount, bool hasProofOfDelivery = false)
    {
        var now = DateTime.UtcNow;
        var legs = shipment.Legs.OrderBy(l => l.Sequence).ToList();
        return new ShipmentResponse
        {
            Id = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            Reference = shipment.Reference,
            Sender = new ReferenceSummary(shipment.SenderParty.Id, shipment.SenderParty.Code, shipment.SenderParty.Name),
            Receiver = new ReferenceSummary(shipment.ReceiverParty.Id, shipment.ReceiverParty.Code, shipment.ReceiverParty.Name),
            Origin = new ReferenceSummary(shipment.OriginLocation.Id, shipment.OriginLocation.Code, shipment.OriginLocation.Name),
            Destination = new ReferenceSummary(
                shipment.DestinationLocation.Id, shipment.DestinationLocation.Code, shipment.DestinationLocation.Name),
            Status = shipment.Status,
            IsInternational = shipment.OriginLocation.Country != shipment.DestinationLocation.Country,
            Incoterm = shipment.Incoterm,
            CustomsStatus = shipment.CustomsStatus,
            PlannedPickupAt = shipment.PlannedPickupAt,
            RequiresSignature = shipment.RequiresSignature,
            HasProofOfDelivery = hasProofOfDelivery,
            EstimatedArrival = legs.LastOrDefault()?.PlannedArrival,
            DeliveredAt = shipment.DeliveredAt,
            CurrentLegSequence = legs.FirstOrDefault(l => !l.ActualArrival.HasValue)?.Sequence,
            ItemCount = itemCount,
            Notes = shipment.Notes,
            Legs = legs.Select(l => ShipmentLegResponse.From(l, now)).ToList(),
            CreatedAt = shipment.CreatedAt,
            UpdatedAt = shipment.UpdatedAt,
            CreatedBy = shipment.CreatedBy
        };
    }
}

// Plan a route for a Planned shipment and replace its legs with it.
public class RouteShipmentRequest
{
    public RouteObjective Objective { get; set; } = RouteObjective.Fastest;

    // Use this exact path (lane ids from a /Routes/plan option) instead of the best one.
    [MaxLength(12)]
    public List<int>? LaneIds { get; set; }

    // When the goods are ready for pickup at the shipment's origin.
    // Defaults to the shipment's PlannedPickupAt, else now.
    public DateTimeOffset? ReadyAt { get; set; }

    // Pickup from a customer address to its station (Courier leg), when the origin is one.
    [Range(0, 2880)]
    public int FirstMileMinutes { get; set; } = 120;

    // Delivery from the destination station to a customer address (Courier leg), when the destination is one.
    [Range(0, 2880)]
    public int LastMileMinutes { get; set; } = 240;
}

public class ShipmentRouteResponse
{
    // The shipment with its new legs.
    public ShipmentResponse Shipment { get; set; } = null!;
    // The station-to-station plan the lane legs came from.
    public RoutePlanResponse Plan { get; set; } = null!;
}
