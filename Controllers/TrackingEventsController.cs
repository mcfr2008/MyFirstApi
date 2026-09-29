using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Events are append-only: there is no update or delete endpoint.
// Permission policies are intentionally not applied yet.
[ApiController]
[Route("api/[controller]")]
public class TrackingEventsController : ControllerBase
{
    private readonly ITrackingEventService _trackingEventService;

    public TrackingEventsController(ITrackingEventService trackingEventService)
    {
        _trackingEventService = trackingEventService;
    }

    // Use trackedItemId/tagCode + oldestFirst=true for an item's timeline.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TrackingEventQuery query)
    {
        var result = await _trackingEventService.GetEventsAsync(query);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Record(RecordEventRequest request)
    {
        var trackingEvent = await _trackingEventService.RecordAsync(request);
        return StatusCode(StatusCodes.Status201Created, trackingEvent);
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan(ScanEventsRequest request)
    {
        var events = await _trackingEventService.ScanAsync(request);
        return StatusCode(StatusCodes.Status201Created, new { eventsRecorded = events.Count, events });
    }
}
