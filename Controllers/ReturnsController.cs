using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Idempotency;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Customer returns (RMA): Requested -> Approved (return shipment) -> Received,
// or Requested -> Rejected / Cancelled. Permission policies are intentionally not applied yet.
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ReturnsController : ControllerBase
{
    private readonly IReturnRequestService _returns;

    public ReturnsController(IReturnRequestService returns)
    {
        _returns = returns;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ReturnRequestQuery query)
    {
        return Ok(await _returns.GetAllAsync(query));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var returnRequest = await _returns.GetByIdAsync(id);
        if (returnRequest == null) return NotFound();

        return Ok(returnRequest);
    }

    [HttpGet("by-rma/{rmaNumber}")]
    public async Task<IActionResult> GetByRmaNumber(string rmaNumber)
    {
        var returnRequest = await _returns.GetByRmaNumberAsync(rmaNumber);
        if (returnRequest == null) return NotFound();

        return Ok(returnRequest);
    }

    [Idempotent]
    [HttpPost]
    public async Task<IActionResult> Create(CreateReturnRequestRequest request)
    {
        var returnRequest = await _returns.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = returnRequest.Id }, returnRequest);
    }

    [Idempotent]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, ApproveReturnRequestRequest request)
    {
        var result = await _returns.ApproveAsync(id, request);
        if (result == null) return NotFound();

        return Ok(result);
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, DecideReturnRequestRequest request)
    {
        var returnRequest = await _returns.RejectAsync(id, request);
        if (returnRequest == null) return NotFound();

        return Ok(returnRequest);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, DecideReturnRequestRequest request)
    {
        var returnRequest = await _returns.CancelAsync(id, request);
        if (returnRequest == null) return NotFound();

        return Ok(returnRequest);
    }
}
