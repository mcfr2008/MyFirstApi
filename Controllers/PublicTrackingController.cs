using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Customer-facing shipment tracking: anyone with the tracking number can follow
// the shipment without logging in, so the response only holds public details.
// Rate limited per client IP to make guessing tracking numbers impractical.
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicy)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class PublicTrackingController : ControllerBase
{
    public const string RateLimitPolicy = "PublicTracking";

    private readonly IPublicTrackingService _publicTrackingService;

    public PublicTrackingController(IPublicTrackingService publicTrackingService)
    {
        _publicTrackingService = publicTrackingService;
    }

    [HttpGet("{trackingNumber}")]
    public async Task<IActionResult> Track(string trackingNumber)
    {
        var result = await _publicTrackingService.TrackAsync(trackingNumber);
        if (result == null) return NotFound();

        return Ok(result);
    }
}
