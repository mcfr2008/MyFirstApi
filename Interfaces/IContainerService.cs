using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IContainerService : IMasterDataService<ContainerRequest, ContainerResponse, ContainerQuery>
{
    Task<ContainerContentsResponse?> GetContentsAsync(int id);
    Task<EventsRecordedResponse?> LoadAsync(int id, ContainerLoadRequest request);
    Task<EventsRecordedResponse?> UnloadAsync(int id, ContainerUnloadRequest request);

    // Records one event for every item inside (including nested containers).
    Task<EventsRecordedResponse?> ScanAsync(int id, EventDetails request);
}
