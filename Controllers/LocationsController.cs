using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/[controller]")]
public class LocationsController : MasterDataController<LocationRequest, LocationResponse, LocationQuery>
{
    public LocationsController(ILocationService service) : base(service)
    {
    }
}
