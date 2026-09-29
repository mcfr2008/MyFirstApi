using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ITrackingEventService
{
    Task<PagedResult<TrackingEventResponse>> GetEventsAsync(TrackingEventQuery query);
    Task<TrackingEventResponse> RecordAsync(RecordEventRequest request);
    Task<IReadOnlyList<TrackingEventResponse>> ScanAsync(ScanEventsRequest request);
}
