using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Endpoints shared by every master-data controller. Derived controllers only
// supply the route and their service. Permission policies are intentionally not
// applied yet; for now every endpoint only requires a logged-in user.
[ApiController]
public abstract class MasterDataController<TRequest, TResponse, TQuery> : ControllerBase
    where TResponse : IMasterDataResponse
    where TQuery : MasterDataQuery
{
    private readonly IMasterDataService<TRequest, TResponse, TQuery> _service;

    protected MasterDataController(IMasterDataService<TRequest, TResponse, TQuery> service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] TQuery query)
    {
        var result = await _service.GetAllAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound();

        return Ok(item);
    }

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var item = await _service.GetByCodeAsync(code);
        if (item == null) return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TRequest request)
    {
        var item = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TRequest request)
    {
        var item = await _service.UpdateAsync(id, request);
        if (item == null) return NotFound();

        return Ok(item);
    }

    // Deactivates rather than deletes, so items/events that reference it stay valid.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var found = await _service.SetActiveAsync(id, false);
        if (!found) return NotFound();

        return NoContent();
    }

    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var found = await _service.SetActiveAsync(id, true);
        if (!found) return NotFound();

        return NoContent();
    }
}
