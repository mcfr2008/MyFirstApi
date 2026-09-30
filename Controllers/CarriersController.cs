using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/[controller]")]
public class CarriersController : MasterDataController<CarrierRequest, CarrierResponse, CarrierQuery>
{
    public CarriersController(ICarrierService service) : base(service)
    {
    }
}
