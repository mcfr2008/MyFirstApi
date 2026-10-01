using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Idempotency;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Events are never updated or deleted: a wrong event is voided (and optionally
// replaced via /correct), keeping the original for audit.
// Permission policies are intentionally not applied yet.
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
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

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var trackingEvent = await _trackingEventService.GetEventByIdAsync(id);
        if (trackingEvent == null) return NotFound();

        return Ok(trackingEvent);
    }

    [Idempotent]
    [HttpPost]
    public async Task<IActionResult> Record(RecordEventRequest request)
    {
        var trackingEvent = await _trackingEventService.RecordAsync(request);
        return StatusCode(StatusCodes.Status201Created, trackingEvent);
    }

    [Idempotent]
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(ScanEventsRequest request)
    {
        var events = await _trackingEventService.ScanAsync(request);
        return StatusCode(StatusCodes.Status201Created, new { eventsRecorded = events.Count, events });
    }

    [Idempotent]
    [HttpPost("{id:long}/void")]
    public async Task<IActionResult> Void(long id, VoidEventRequest request)
    {
        var trackingEvent = await _trackingEventService.VoidAsync(id, request);
        if (trackingEvent == null) return NotFound();

        return Ok(trackingEvent);
    }

    // Void + record the corrected event in one step.
    [Idempotent]
    [HttpPost("{id:long}/correct")]
    public async Task<IActionResult> Correct(long id, CorrectEventRequest request)
    {
        var result = await _trackingEventService.CorrectAsync(id, request);
        if (result == null) return NotFound();

        return Ok(result);
    }
}
