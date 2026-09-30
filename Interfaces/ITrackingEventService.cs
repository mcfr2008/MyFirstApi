using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ITrackingEventService
{
    Task<CursorPagedResult<TrackingEventResponse>> GetEventsAsync(TrackingEventQuery query);
    Task<TrackingEventResponse> RecordAsync(RecordEventRequest request);
    Task<IReadOnlyList<TrackingEventResponse>> ScanAsync(ScanEventsRequest request);
    Task<TrackingEventResponse?> GetEventByIdAsync(long id);
    Task<TrackingEventResponse?> VoidAsync(long id, VoidEventRequest request);
    Task<CorrectEventResponse?> CorrectAsync(long id, CorrectEventRequest request);
}
