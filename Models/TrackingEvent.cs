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

// One entry in an item's history. Events are never updated or deleted: a wrong
// event is voided (kept, flagged, with who/when/why) and optionally replaced.
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
    public int? ReasonCodeId { get; set; }
    public ReasonCode? ReasonCode { get; set; }

    // Misroute: the open shipment whose route doesn't include this event's location.
    // Set at record time (TrackingEventRecorder); null = on route or not checked.
    public int? OffRouteShipmentId { get; set; }
    public Shipment? OffRouteShipment { get; set; }

    // Recorded by a shipment/container operation (leg departure, customs,
    // delivery, load/unload). Can't be voided directly - undo the operation instead.
    public bool IsSystemManaged { get; set; }

    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    // Set on a correction: the voided event this one replaces.
    public long? ReplacesEventId { get; set; }
}
