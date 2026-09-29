using MyFirstApi.Dtos;

namespace MyFirstApi.Interfaces;

public interface ICarrierService : IMasterDataService<CarrierRequest, CarrierResponse, CarrierQuery>
{
}
