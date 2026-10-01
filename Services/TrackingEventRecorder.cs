using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class TrackingEventRecorder : ITrackingEventRecorder
{
    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IMasterDataCache _cache;

    public TrackingEventRecorder(AppDbContext context, ICurrentUser currentUser, IMasterDataCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<EventType> GetEventTypeAsync(string code)
    {
        var normalized = QueryHelpers.NormalizeCode(code);
        var eventType = await _cache.FindEventTypeAsync(normalized);

        if (eventType == null)
        {
            throw Errors.EventTypeNotConfigured(normalized);
        }
        if (!eventType.IsActive)
        {
            throw Errors.EventTypeInactive(normalized);
        }
        return eventType;
    }

    public async Task<List<TrackedItem>> FindItemsAsync(IEnumerable<int>? ids, IEnumerable<string>? tagCodes)
    {
        var idList = ids?.Distinct().ToList() ?? [];
        var tagList = tagCodes?.Select(QueryHelpers.NormalizeCode).Distinct().ToList() ?? [];

        var items = await _context.TrackedItems
            .Where(i => idList.Contains(i.Id) || tagList.Contains(i.TagCode))
            .ToListAsync();

        var missing = idList.Where(id => items.All(i => i.Id != id)).Select(id => id.ToString())
            .Concat(tagList.Where(tag => items.All(i => i.TagCode != tag)))
            .ToList();
        if (missing.Count > 0)
        {
            throw Errors.ItemsNotFound(missing);
        }

        EnsureNotArchived(items);
        return items;
    }

    public async Task<List<TrackedItem>> FindItemsInContainersAsync(IEnumerable<int> containerIds)
    {
        var allIds = new HashSet<int>();
        foreach (var id in containerIds)
        {
            allIds.UnionWith(await GetContainerTreeIdsAsync(id));
        }

        var items = await _context.TrackedItems
            .Where(i => i.CurrentContainerId != null && allIds.Contains(i.CurrentContainerId.Value))
            .ToListAsync();

        EnsureNotArchived(items);
        return items;
    }

    // Breadth-first walk down ParentContainerId links (one query per nesting level).
    public async Task<List<int>> GetContainerTreeIdsAsync(int rootContainerId)
    {
        var result = new List<int> { rootContainerId };
        var level = new List<int> { rootContainerId };

        while (level.Count > 0)
        {
            var current = level;
            level = await _context.Containers
                .Where(c => c.ParentContainerId != null && current.Contains(c.ParentContainerId.Value))
                .Select(c => c.Id)
                .ToListAsync();
            level.RemoveAll(result.Contains);
            result.AddRange(level);
        }
        return result;
    }

    public async Task<List<TrackingEvent>> AddEventsAsync(
        IReadOnlyCollection<TrackedItem> items, EventType eventType, EventContext context)
    {
        if (items.Count == 0)
        {
            throw Errors.NoItemsForEvent();
        }
        EnsureNotArchived(items);

        await ReferenceResolver.ResolveAsync(_context.Locations.AsNoTracking(), context.LocationId, null, "locationId");
        var reason = await ResolveReasonAsync(eventType, context);

        var offRoute = await FindOffRouteShipmentsAsync(items, context);
        var now = DateTime.UtcNow;
        var events = new List<TrackingEvent>(items.Count);

        foreach (var item in items)
        {
            var trackingEvent = new TrackingEvent
            {
                TrackedItem = item,
                // Id only: eventType may be a detached cached instance.
                EventTypeId = eventType.Id,
                ReasonCodeId = reason?.Id,
                IsSystemManaged = context.IsSystemManaged,
                LocationId = context.LocationId,
                OccurredAt = context.OccurredAt,
                RecordedAt = now,
                RecordedBy = _currentUser.Username,
                Source = context.Source,
                Note = QueryHelpers.NullIfBlank(context.Note),
                Latitude = context.Latitude,
                Longitude = context.Longitude,
                ShipmentId = context.ShipmentId,
                ShipmentLegId = context.ShipmentLegId,
                ContainerId = context.ContainerId,
                OffRouteShipmentId = offRoute.GetValueOrDefault(item.Id)
            };
            events.Add(trackingEvent);
            ApplyToItem(item, eventType, context, now);
        }

        _context.TrackingEvents.AddRange(events);
        return events;
    }

    public async Task RecalculateItemStateAsync(TrackedItem item)
    {
        // Tracked instances keep in-memory changes (e.g. IsVoided just set), so
        // filter again after loading; unsaved events come from the change tracker.
        var saved = await _context.TrackingEvents
            .Include(e => e.EventType)
            .Where(e => e.TrackedItemId == item.Id && !e.IsVoided)
            .ToListAsync();
        var unsaved = _context.ChangeTracker.Entries<TrackingEvent>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.TrackedItem == item)
            .Select(entry => entry.Entity);

        var timeline = new List<(TrackingEvent Event, ItemStatus? ResultingStatus)>();
        foreach (var trackingEvent in saved.Where(e => !e.IsVoided).Concat(unsaved))
        {
            // Unsaved events only carry EventTypeId (see AddEventsAsync).
            var eventType = trackingEvent.EventType ?? await _cache.FindEventTypeAsync(trackingEvent.EventTypeId);
            timeline.Add((trackingEvent, eventType?.ResultingStatus));
        }
        timeline = timeline
            .OrderBy(t => t.Event.OccurredAt)
            .ThenBy(t => t.Event.Id == 0 ? long.MaxValue : t.Event.Id)
            .ToList();

        item.Status = timeline.LastOrDefault(t => t.ResultingStatus.HasValue).ResultingStatus ?? ItemStatus.Registered;
        item.CurrentLocationId = timeline.LastOrDefault(t => t.Event.LocationId.HasValue).Event?.LocationId
                                 ?? item.CurrentLocationId;
        item.LastEventAt = timeline.Count > 0 ? timeline[^1].Event.OccurredAt : null;
        item.UpdatedAt = DateTime.UtcNow;
    }

    // Misroute check: for items in an open (Planned / InTransit) shipment that has legs,
    // the item -> shipment id where the event's location isn't on that shipment's route
    // (its origin, destination, or any leg's origin / destination). Events recorded by a
    // shipment operation follow its route by definition and aren't checked.
    private async Task<Dictionary<int, int?>> FindOffRouteShipmentsAsync(
        IReadOnlyCollection<TrackedItem> items, EventContext context)
    {
        var result = new Dictionary<int, int?>();
        if (context.LocationId == null || context.ShipmentId != null) return result;

        var itemIds = items.Where(i => i.Id != 0).Select(i => i.Id).ToList();
        if (itemIds.Count == 0) return result;

        var location = context.LocationId.Value;
        var memberships = await _context.ShipmentItems.AsNoTracking()
            .Where(si => itemIds.Contains(si.TrackedItemId) &&
                         (si.Shipment.Status == ShipmentStatus.Planned || si.Shipment.Status == ShipmentStatus.InTransit) &&
                         si.Shipment.Legs.Any())
            .Select(si => new
            {
                si.TrackedItemId,
                si.ShipmentId,
                OnRoute = si.Shipment.OriginLocationId == location ||
                          si.Shipment.DestinationLocationId == location ||
                          si.Shipment.Legs.Any(l => l.OriginLocationId == location || l.DestinationLocationId == location)
            })
            .ToListAsync();

        foreach (var membership in memberships.Where(m => !m.OnRoute))
        {
            result[membership.TrackedItemId] = membership.ShipmentId;
        }
        return result;
    }

    private async Task<ReasonCode?> ResolveReasonAsync(EventType eventType, EventContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ReasonCode))
        {
            if (eventType.RequiresReason)
            {
                throw Errors.ReasonRequired(eventType.Code);
            }
            return null;
        }

        var code = QueryHelpers.NormalizeCode(context.ReasonCode);
        var reason = await _cache.FindReasonCodeAsync(code);
        if (reason == null)
        {
            throw Errors.ReasonNotFound(code);
        }
        if (!reason.IsActive)
        {
            throw Errors.ReasonInactive(code);
        }
        if (reason.EventTypeCodes.Count > 0 && !reason.EventTypeCodes.Contains(eventType.Code))
        {
            throw Errors.ReasonNotAllowed(code, eventType.Code, reason.EventTypeCodes);
        }
        if (reason.RequiresNote && string.IsNullOrWhiteSpace(context.Note))
        {
            throw Errors.ReasonNoteRequired(code);
        }
        return reason;
    }

    // An event updates the item's current state only if it is the newest one;
    // a back-dated event is kept in the history without rewinding the item.
    private static void ApplyToItem(TrackedItem item, EventType eventType, EventContext context, DateTime now)
    {
        if (item.LastEventAt.HasValue && context.OccurredAt < item.LastEventAt.Value) return;

        if (eventType.ResultingStatus.HasValue)
        {
            item.Status = eventType.ResultingStatus.Value;
        }
        if (context.LocationId.HasValue)
        {
            item.CurrentLocationId = context.LocationId;
        }
        item.LastEventAt = context.OccurredAt;
        item.UpdatedAt = now;
    }

    private static void EnsureNotArchived(IEnumerable<TrackedItem> items)
    {
        var archived = items.Where(i => i.IsArchived).Select(i => i.TagCode).ToList();
        if (archived.Count > 0)
        {
            throw Errors.ItemsArchived(archived);
        }
    }
}
