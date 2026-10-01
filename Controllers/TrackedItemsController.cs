using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Idempotency;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Status and location change through tracking events (/api/TrackingEvents),
// not through this controller. Permission policies are intentionally not
// applied yet; for now every endpoint only requires a logged-in user.
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class TrackedItemsController : ControllerBase
{
    private readonly ITrackedItemService _trackedItemService;

    public TrackedItemsController(ITrackedItemService trackedItemService)
    {
        _trackedItemService = trackedItemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TrackedItemQuery query)
    {
        var result = await _trackedItemService.GetItemsAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _trackedItemService.GetItemByIdAsync(id);
        if (item == null) return NotFound();

        return Ok(item);
    }

    [HttpGet("by-tag/{tagCode}")]
    public async Task<IActionResult> GetByTagCode(string tagCode)
    {
        var item = await _trackedItemService.GetItemByTagCodeAsync(tagCode);
        if (item == null) return NotFound();

        return Ok(item);
    }

    [Idempotent]
    [HttpPost]
    public async Task<IActionResult> Create(CreateTrackedItemRequest request)
    {
        var item = await _trackedItemService.CreateItemAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [Idempotent]
    [HttpPost("bulk")]
    public async Task<IActionResult> CreateBulk(BulkCreateTrackedItemsRequest request)
    {
        var items = await _trackedItemService.CreateItemsAsync(request.Items);
        return Ok(new { createdCount = items.Count, items });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTrackedItemRequest request)
    {
        var item = await _trackedItemService.UpdateItemAsync(id, request);
        if (item == null) return NotFound();

        return Ok(item);
    }

    // Archives rather than deletes, so the item's history is kept.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Archive(int id)
    {
        var archived = await _trackedItemService.ArchiveItemAsync(id);
        if (!archived) return NotFound();

        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var restored = await _trackedItemService.RestoreItemAsync(id);
        if (!restored) return NotFound();

        return NoContent();
    }

}
