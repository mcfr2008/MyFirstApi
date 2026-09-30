using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

// Shared contract for master-data tables (item categories, locations, parties, event types).
public interface IMasterDataService<TRequest, TResponse, TQuery>
    where TResponse : IMasterDataResponse
    where TQuery : MasterDataQuery
{
    Task<PagedResult<TResponse>> GetAllAsync(TQuery query);
    Task<TResponse?> GetByIdAsync(int id);
    Task<TResponse?> GetByCodeAsync(string code);
    Task<TResponse> CreateAsync(TRequest request);
    Task<TResponse?> UpdateAsync(int id, TRequest request);
    Task<bool> SetActiveAsync(int id, bool isActive);
}
