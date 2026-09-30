using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface IVehicleService : IMasterDataService<VehicleRequest, VehicleResponse, VehicleQuery>
{
}
