using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IServiceAreaService : IMasterDataService<ServiceAreaRequest, ServiceAreaResponse, ServiceAreaQuery>
{
    // The active area covering an address: its postal code first, then its province.
    Task<ServiceAreaResolveResponse> ResolveAsync(ServiceAreaResolveQuery query);
}
