using System.ComponentModel.DataAnnotations;
using MyFirstApi.Models;

namespace MyFirstApi.Dtos;

// What happened, where and when - shared by every way of recording events.
public class EventDetails : IValidatableObject
{
    // Allowed clock skew for OccurredAt values slightly in the future.
    private static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(10);

    // Code from /api/EventTypes, e.g. ARRIVED_AT_HUB.
    [Required]
    [StringLength(50)]
    public string EventTypeCode { get; set; } = string.Empty;

    public int? LocationId { get; set; }

    // Defaults to now. Include an offset (e.g. 2026-09-29T14:30:00+07:00); it is stored as UTC.
    public DateTimeOffset? OccurredAt { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    [Range(-90.0, 90.0)]
    public decimal? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public decimal? Longitude { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OccurredAt.HasValue && OccurredAt.Value > DateTimeOffset.UtcNow + MaxFutureSkew)
        {
            yield return new ValidationResult("OccurredAt cannot be in the future.", [nameof(OccurredAt)]);
        }
    }
}

public class RecordEventRequest : EventDetails
{
    // Identify the item by id or by tag code (exactly one).
    public int? TrackedItemId { get; set; }

    [StringLength(64)]
    public string? TagCode { get; set; }

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;

        if (TrackedItemId.HasValue == !string.IsNullOrWhiteSpace(TagCode))
        {
            yield return new ValidationResult(
                "Provide either TrackedItemId or TagCode.", [nameof(TrackedItemId), nameof(TagCode)]);
        }
    }
}

// Several tags scanned at the same place and time.
public class ScanEventsRequest : EventDetails
{
    public const int MaxTags = 500;

    [Required]
    [MinLength(1)]
    [MaxLength(MaxTags)]
    public List<string> TagCodes { get; set; } = new();
}

public class TrackingEventQuery : PagedQuery
{
    public int? TrackedItemId { get; set; }
    public string? TagCode { get; set; }
    public int? ShipmentId { get; set; }
    public int? ContainerId { get; set; }
    public int? LocationId { get; set; }
    public string? EventTypeCode { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }

    // true = timeline order (oldest first); default is newest first.
    public bool OldestFirst { get; set; }
}

public class TrackingEventResponse
{
    public long Id { get; set; }
    public ReferenceSummary TrackedItem { get; set; } = null!;
    public EventTypeSummary EventType { get; set; } = null!;
    public ItemStatus? ResultingStatus { get; set; }
    public ReferenceSummary? Location { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? RecordedBy { get; set; }
    public EventSource Source { get; set; }
    public string? Note { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public ReferenceSummary? Shipment { get; set; }
    public int? ShipmentLegId { get; set; }
    public ReferenceSummary? Container { get; set; }
}

// Returned by operations that record the same event for many items.
public record EventsRecordedResponse(string EventTypeCode, int EventsRecorded);
