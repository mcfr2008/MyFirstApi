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

    public TrackingEventRecorder(AppDbContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<EventType> GetEventTypeAsync(string code)
    {
        var normalized = QueryHelpers.NormalizeCode(code);
        var eventType = await _context.EventTypes.FirstOrDefaultAsync(e => e.Code == normalized);

        if (eventType == null)
        {
            throw new BusinessRuleException($"Event type {normalized} is not configured (see /api/EventTypes).");
        }
        if (!eventType.IsActive)
        {
            throw new BusinessRuleException($"Event type {normalized} is inactive.");
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
            throw new BusinessRuleException($"Tracked items not found: {string.Join(", ", missing)}");
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
            throw new BusinessRuleException("There are no items to record the event for.");
        }
        EnsureNotArchived(items);

        await ReferenceResolver.ResolveAsync(_context.Locations.AsNoTracking(), context.LocationId, null, "Location");

        var now = DateTime.UtcNow;
        var events = new List<TrackingEvent>(items.Count);

        foreach (var item in items)
        {
            var trackingEvent = new TrackingEvent
            {
                TrackedItem = item,
                EventTypeId = eventType.Id,
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
                ContainerId = context.ContainerId
            };
            events.Add(trackingEvent);
            ApplyToItem(item, eventType, context, now);
        }

        _context.TrackingEvents.AddRange(events);
        return events;
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
            throw new BusinessRuleException($"Archived items can't be tracked: {string.Join(", ", archived)}");
        }
    }
}
