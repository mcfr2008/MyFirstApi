using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class LanesController : MasterDataController<LaneRequest, LaneResponse, LaneQuery>
{
    public LanesController(ILaneService service) : base(service)
    {
    }
}
