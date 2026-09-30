using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyFirstApi.Data;
using MyFirstApi.Dtos;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Services;

public class TrackingEventService : ITrackingEventService
{
    private readonly AppDbContext _context;
    private readonly ITrackingEventRecorder _recorder;
    private readonly ICurrentUser _currentUser;

    public TrackingEventService(AppDbContext context, ITrackingEventRecorder recorder, ICurrentUser currentUser)
    {
        _context = context;
        _recorder = recorder;
        _currentUser = currentUser;
    }

    // Instance property (not static): ReplacedByEventId is a subquery on the context.
    private Expression<Func<TrackingEvent, TrackingEventResponse>> Projection => e => new TrackingEventResponse
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
            : new ReferenceSummary(e.Container.Id, e.Container.Code, e.Container.Type.ToString()),
        Reason = e.ReasonCode == null
            ? null
            : new ReasonSummary(e.ReasonCode.Id, e.ReasonCode.Code, e.ReasonCode.NameTh, e.ReasonCode.NameEn),
        IsSystemManaged = e.IsSystemManaged,
        IsVoided = e.IsVoided,
        VoidedAt = e.VoidedAt,
        VoidedBy = e.VoidedBy,
        VoidReason = e.VoidReason,
        ReplacesEventId = e.ReplacesEventId,
        // Only voided events can have a replacement; the CASE skips the lookup
        // (which touches every partition) for all other rows.
        ReplacedByEventId = e.IsVoided
            ? _context.TrackingEvents
                .Where(r => r.ReplacesEventId == e.Id)
                .Select(r => (long?)r.Id)
                .FirstOrDefault()
            : null
    };

    public async Task<CursorPagedResult<TrackingEventResponse>> GetEventsAsync(TrackingEventQuery query)
    {
        var events = _context.TrackingEvents.AsNoTracking();

        if (!query.IncludeVoided)
        {
            events = events.Where(e => !e.IsVoided);
        }
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
        if (!string.IsNullOrWhiteSpace(query.ReasonCode))
        {
            var reasonCode = QueryHelpers.NormalizeCode(query.ReasonCode);
            events = events.Where(e => e.ReasonCode != null && e.ReasonCode.Code == reasonCode);
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

        int? totalCount = query.IncludeTotalCount ? await events.CountAsync() : null;

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var (afterTime, afterId) = DecodeCursor(query.Cursor);
            events = query.OldestFirst
                ? events.Where(e => e.OccurredAt > afterTime || (e.OccurredAt == afterTime && e.Id > afterId))
                : events.Where(e => e.OccurredAt < afterTime || (e.OccurredAt == afterTime && e.Id < afterId));
        }

        var ordered = query.OldestFirst
            ? events.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            : events.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id);

        // One extra row tells us whether there is a next page without counting.
        var page = await ordered
            .Take(query.PageSize + 1)
            .Select(Projection)
            .ToListAsync();

        var hasMore = page.Count > query.PageSize;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        return new CursorPagedResult<TrackingEventResponse>
        {
            Items = page,
            PageSize = query.PageSize,
            HasMore = hasMore,
            NextCursor = hasMore ? EncodeCursor(page[^1].OccurredAt, page[^1].Id) : null,
            TotalCount = totalCount
        };
    }

    // Cursor = position of the last row returned: "<OccurredAt ticks>_<Id>", base64url-encoded.
    private static string EncodeCursor(DateTime occurredAt, long id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{occurredAt.Ticks}_{id}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static (DateTime OccurredAt, long Id) DecodeCursor(string cursor)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('_');
            return (new DateTime(long.Parse(parts[0]), DateTimeKind.Utc), long.Parse(parts[1]));
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException or OverflowException)
        {
            throw new BusinessRuleException("Invalid cursor. Use nextCursor from the previous page.");
        }
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
            details.Longitude,
            ReasonCode: details.ReasonCode);

        var events = await _recorder.AddEventsAsync(items, eventType, context);
        await _context.SaveChangesAsync();

        return await GetByIdsAsync(events.Select(e => e.Id).ToList(), context.OccurredAt);
    }

    public async Task<TrackingEventResponse?> GetEventByIdAsync(long id) =>
        await _context.TrackingEvents.AsNoTracking().Where(e => e.Id == id).Select(Projection).FirstOrDefaultAsync();

    // The item's status/location are rebuilt from its remaining events.
    public async Task<TrackingEventResponse?> VoidAsync(long id, VoidEventRequest request)
    {
        var original = await LoadVoidableAsync(id);
        if (original == null) return null;

        MarkVoided(original, request.Reason);
        await _recorder.RecalculateItemStateAsync(original.TrackedItem);
        await _context.SaveChangesAsync();

        return await GetEventByIdAsync(id);
    }

    public async Task<CorrectEventResponse?> CorrectAsync(long id, CorrectEventRequest request)
    {
        var original = await LoadVoidableAsync(id);
        if (original == null) return null;

        var eventType = string.IsNullOrWhiteSpace(request.EventTypeCode)
            ? original.EventType
            : await _recorder.GetEventTypeAsync(request.EventTypeCode);
        var sameType = eventType.Id == original.EventTypeId;

        MarkVoided(original, request.Reason);

        var context = new EventContext(
            request.LocationId ?? original.LocationId,
            request.OccurredAt?.UtcDateTime ?? original.OccurredAt,
            original.Source,
            request.Note ?? original.Note,
            request.Latitude ?? original.Latitude,
            request.Longitude ?? original.Longitude,
            original.ShipmentId,
            original.ShipmentLegId,
            original.ContainerId,
            request.ReasonCode ?? (sameType ? original.ReasonCode?.Code : null));
        var replacement = (await _recorder.AddEventsAsync([original.TrackedItem], eventType, context))[0];
        replacement.ReplacesEventId = original.Id;

        await _recorder.RecalculateItemStateAsync(original.TrackedItem);
        await _context.SaveChangesAsync();

        return new CorrectEventResponse((await GetEventByIdAsync(id))!, (await GetEventByIdAsync(replacement.Id))!);
    }

    private async Task<TrackingEvent?> LoadVoidableAsync(long id)
    {
        var trackingEvent = await _context.TrackingEvents
            .Include(e => e.EventType)
            .Include(e => e.ReasonCode)
            .Include(e => e.TrackedItem)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (trackingEvent == null) return null;

        if (trackingEvent.IsVoided)
        {
            throw new ConflictException("Event is already voided.");
        }
        if (trackingEvent.IsSystemManaged)
        {
            throw new ConflictException(
                "This event was recorded by a shipment or container operation; undo it there " +
                "(e.g. unload the container) instead of voiding it.");
        }
        if (trackingEvent.TrackedItem.IsArchived)
        {
            throw new BusinessRuleException("Events of archived items can't be changed. Restore the item first.");
        }
        return trackingEvent;
    }

    private void MarkVoided(TrackingEvent trackingEvent, string reason)
    {
        trackingEvent.IsVoided = true;
        trackingEvent.VoidedAt = DateTime.UtcNow;
        trackingEvent.VoidedBy = _currentUser.Username;
        trackingEvent.VoidReason = reason.Trim();
    }

    // occurredAt lets PostgreSQL go straight to one partition instead of
    // probing every monthly partition for the ids.
    private async Task<List<TrackingEventResponse>> GetByIdsAsync(List<long> ids, DateTime occurredAt)
    {
        return await _context.TrackingEvents
            .Where(e => e.OccurredAt == occurredAt && ids.Contains(e.Id))
            .OrderBy(e => e.Id)
            .Select(Projection)
            .ToListAsync();
    }
}
