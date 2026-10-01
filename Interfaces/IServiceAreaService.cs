using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IServiceAreaService : IMasterDataService<ServiceAreaRequest, ServiceAreaResponse, ServiceAreaQuery>
{
    // The active area covering an address: its postal code first, then its province.
    Task<ServiceAreaResolveResponse> ResolveAsync(ServiceAreaResolveQuery query);

    // Same lookup for an address; null when no active area covers it.
    Task<ServiceAreaResolveResponse?> FindAsync(string country, string? postalCode, string? province);
}
