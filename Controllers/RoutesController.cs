using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Route planning over the lane network. Read-only: plans aren't saved and
// shipments aren't changed.
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly IRoutePlannerService _routePlanner;

    public RoutesController(IRoutePlannerService routePlanner)
    {
        _routePlanner = routePlanner;
    }

    // POST because the request is a structured query (two endpoints + options), not a new resource.
    [HttpPost("plan")]
    public async Task<IActionResult> Plan(RoutePlanRequest request)
    {
        var plan = await _routePlanner.PlanAsync(request);
        return Ok(plan);
    }
}
