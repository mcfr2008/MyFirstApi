using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class VehiclesController : MasterDataController<VehicleRequest, VehicleResponse, VehicleQuery>
{
    public VehiclesController(IVehicleService service) : base(service)
    {
    }
}
