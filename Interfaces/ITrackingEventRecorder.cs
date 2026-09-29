using MyFirstApi.Models;

namespace MyFirstApi.Interfaces;

// Where/when/why for a batch of events recorded together.
public record EventContext(
    int? LocationId,
    DateTime OccurredAt,
    EventSource Source,
    string? Note = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? ShipmentId = null,
    int? ShipmentLegId = null,
    int? ContainerId = null);

// Building block used by every service that records events (items, scans,
// containers, shipments). It adds entities to the DbContext but never calls
// SaveChanges, so the caller commits events together with its own changes.
public interface ITrackingEventRecorder
{
    Task<EventType> GetEventTypeAsync(string code);

    // Tracked (for update) and not archived; throws listing any id/tag not found.
    Task<List<TrackedItem>> FindItemsAsync(IEnumerable<int>? ids, IEnumerable<string>? tagCodes);

    // Every item in the given containers, including containers nested inside them.
    Task<List<TrackedItem>> FindItemsInContainersAsync(IEnumerable<int> containerIds);

    Task<List<int>> GetContainerTreeIdsAsync(int rootContainerId);

    Task<List<TrackingEvent>> AddEventsAsync(IReadOnlyCollection<TrackedItem> items, EventType eventType, EventContext context);
}
