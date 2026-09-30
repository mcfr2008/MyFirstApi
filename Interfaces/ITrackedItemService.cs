using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ITrackedItemService
{
    Task<PagedResult<TrackedItemResponse>> GetItemsAsync(TrackedItemQuery query);
    Task<TrackedItemResponse?> GetItemByIdAsync(int id);
    Task<TrackedItemResponse?> GetItemByTagCodeAsync(string tagCode);
    Task<TrackedItemResponse> CreateItemAsync(CreateTrackedItemRequest request);
    Task<IReadOnlyList<TrackedItemResponse>> CreateItemsAsync(IReadOnlyList<CreateTrackedItemRequest> requests);
    Task<TrackedItemResponse?> UpdateItemAsync(int id, UpdateTrackedItemRequest request);
    Task<bool> ArchiveItemAsync(int id);
    Task<bool> RestoreItemAsync(int id);
}
