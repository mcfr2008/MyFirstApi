using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class ServiceAreasController : MasterDataController<ServiceAreaRequest, ServiceAreaResponse, ServiceAreaQuery>
{
    private readonly IServiceAreaService _service;

    public ServiceAreasController(IServiceAreaService service) : base(service)
    {
        _service = service;
    }

    // Which station and hub serve an address: ?postalCode=50200&province=เชียงใหม่ or ?locationId=12.
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] ServiceAreaResolveQuery query)
    {
        var result = await _service.ResolveAsync(query);
        return Ok(result);
    }
}
