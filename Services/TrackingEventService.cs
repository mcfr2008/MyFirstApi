using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class TrackingEventService : ITrackingEventService
{
    private readonly AppDbContext _context;
    private readonly ITrackingEventRecorder _recorder;

    public TrackingEventService(AppDbContext context, ITrackingEventRecorder recorder)
    {
        _context = context;
        _recorder = recorder;
    }

    private static readonly Expression<Func<TrackingEvent, TrackingEventResponse>> Projection = e => new TrackingEventResponse
    {
        Id = e.Id,
        TrackedItem = new ReferenceSummary(e.TrackedItem.Id, e.TrackedItem.TagCode, e.TrackedItem.Name),
        EventType = new EventTypeSummary(e.EventType.Id, e.EventType.Code, e.EventType.NameTh, e.EventType.NameEn),
        ResultingStatus = e.EventType.ResultingStatus,
        Location = e.Location == null ? null : new ReferenceSummary(e.Location.Id, e.Location.Code, e.Location.Name),
        OccurredAt = e.OccurredAt,
        RecordedAt = e.RecordedAt,
        RecordedBy = e.RecordedBy,
        Source = e.Source,
        Note = e.Note,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        Shipment = e.Shipment == null
            ? null
            : new ReferenceSummary(e.Shipment.Id, e.Shipment.TrackingNumber, e.Shipment.TrackingNumber),
        ShipmentLegId = e.ShipmentLegId,
        Container = e.Container == null
            ? null
            : new ReferenceSummary(e.Container.Id, e.Container.Code, e.Container.Type.ToString())
    };

    public async Task<PagedResult<TrackingEventResponse>> GetEventsAsync(TrackingEventQuery query)
    {
        var events = _context.TrackingEvents.AsNoTracking();

        if (query.TrackedItemId.HasValue)
        {
            events = events.Where(e => e.TrackedItemId == query.TrackedItemId);
        }
        if (!string.IsNullOrWhiteSpace(query.TagCode))
        {
            var tagCode = QueryHelpers.NormalizeCode(query.TagCode);
            events = events.Where(e => e.TrackedItem.TagCode == tagCode);
        }
        if (query.ShipmentId.HasValue)
        {
            events = events.Where(e => e.ShipmentId == query.ShipmentId);
        }
        if (query.ContainerId.HasValue)
        {
            events = events.Where(e => e.ContainerId == query.ContainerId);
        }
        if (query.LocationId.HasValue)
        {
            events = events.Where(e => e.LocationId == query.LocationId);
        }
        if (!string.IsNullOrWhiteSpace(query.EventTypeCode))
        {
            var code = QueryHelpers.NormalizeCode(query.EventTypeCode);
            events = events.Where(e => e.EventType.Code == code);
        }
        if (query.From.HasValue)
        {
            var from = query.From.Value.UtcDateTime;
            events = events.Where(e => e.OccurredAt >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.UtcDateTime;
            events = events.Where(e => e.OccurredAt <= to);
        }

        var ordered = query.OldestFirst
            ? events.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            : events.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id);

        var totalCount = await events.CountAsync();
        var page = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(Projection)
            .ToListAsync();

        return new PagedResult<TrackingEventResponse>
        {
            Items = page,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TrackingEventResponse> RecordAsync(RecordEventRequest request)
    {
        var items = await _recorder.FindItemsAsync(
            request.TrackedItemId.HasValue ? [request.TrackedItemId.Value] : null,
            string.IsNullOrWhiteSpace(request.TagCode) ? null : [request.TagCode]);

        var created = await RecordForItemsAsync(items, request, EventSource.Manual);
        return created[0];
    }

    // All-or-nothing: an unknown or archived tag rejects the whole scan.
    public async Task<IReadOnlyList<TrackingEventResponse>> ScanAsync(ScanEventsRequest request)
    {
        var items = await _recorder.FindItemsAsync(null, request.TagCodes);
        return await RecordForItemsAsync(items, request, EventSource.Scan);
    }

    private async Task<List<TrackingEventResponse>> RecordForItemsAsync(
        List<TrackedItem> items, EventDetails details, EventSource source)
    {
        var eventType = await _recorder.GetEventTypeAsync(details.EventTypeCode);
        var context = new EventContext(
            details.LocationId,
            details.OccurredAt?.UtcDateTime ?? DateTime.UtcNow,
            source,
            details.Note,
            details.Latitude,
            details.Longitude);

        var events = await _recorder.AddEventsAsync(items, eventType, context);
        await _context.SaveChangesAsync();

        var ids = events.Select(e => e.Id).ToList();
        return await _context.TrackingEvents
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Id)
            .Select(Projection)
            .ToListAsync();
    }
}
