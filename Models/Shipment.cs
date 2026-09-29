using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ShipmentStatus>))]
public enum ShipmentStatus
{
    Planned,
    InTransit,
    Delivered,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter<CustomsStatus>))]
public enum CustomsStatus
{
    NotRequired,
    Pending,
    InProgress,
    Hold,
    Cleared
}

// A consignment from a sender at an origin to a receiver at a destination,
// moved over one or more legs (road -> sea -> road, ...).
public class Shipment
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    // Customer's own reference (PO number, order number, ...).
    public string? Reference { get; set; }
    public int SenderPartyId { get; set; }
    public Party SenderParty { get; set; } = null!;
    public int ReceiverPartyId { get; set; }
    public Party ReceiverParty { get; set; } = null!;
    public int OriginLocationId { get; set; }
    public Location OriginLocation { get; set; } = null!;
    public int DestinationLocationId { get; set; }
    public Location DestinationLocation { get; set; } = null!;
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Planned;
    // Incoterms 2020 rule (FOB, CIF, DAP, ...), international shipments only.
    public string? Incoterm { get; set; }
    public CustomsStatus CustomsStatus { get; set; } = CustomsStatus.NotRequired;
    public DateTime? PlannedPickupAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public List<ShipmentLeg> Legs { get; set; } = new();
    public List<ShipmentItem> Items { get; set; } = new();
}

public class ShipmentItem
{
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public int TrackedItemId { get; set; }
    public TrackedItem TrackedItem { get; set; } = null!;
    public DateTime AddedAt { get; set; }
}
