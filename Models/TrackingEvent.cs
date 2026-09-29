using System.Text.Json.Serialization;

namespace MyFirstApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter<EventSource>))]
public enum EventSource
{
    // Recorded for a single item.
    Manual,
    // Bulk scan of several tags at one location.
    Scan,
    // Recorded for everything inside a container.
    Container,
    // Recorded for every item in a shipment (leg departure/arrival, customs, delivery).
    Shipment
}

// One entry in an item's history. Events are append-only: never updated or deleted.
public class TrackingEvent
{
    public long Id { get; set; }
    public int TrackedItemId { get; set; }
    public TrackedItem TrackedItem { get; set; } = null!;
    public int EventTypeId { get; set; }
    public EventType EventType { get; set; } = null!;
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    // When it happened (may be back-dated) vs. when it was entered.
    public DateTime OccurredAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? RecordedBy { get; set; }
    public EventSource Source { get; set; }
    public string? Note { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public int? ShipmentLegId { get; set; }
    public ShipmentLeg? ShipmentLeg { get; set; }
    public int? ContainerId { get; set; }
    public Container? Container { get; set; }
}
