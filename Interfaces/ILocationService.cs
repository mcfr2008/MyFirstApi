using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ILocationService : IMasterDataService<LocationRequest, LocationResponse, LocationQuery>
{
}
