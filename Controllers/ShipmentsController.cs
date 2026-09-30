using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Lifecycle: Planned -> (legs depart/arrive) InTransit -> Delivered, or Planned -> Cancelled.
// Permission policies are intentionally not applied yet.
[ApiController]
[Route("api/[controller]")]
public class ShipmentsController : ControllerBase
{
    private readonly IShipmentService _shipmentService;

    public ShipmentsController(IShipmentService shipmentService)
    {
        _shipmentService = shipmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ShipmentQuery query)
    {
        var result = await _shipmentService.GetShipmentsAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var shipment = await _shipmentService.GetShipmentByIdAsync(id);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpGet("by-tracking/{trackingNumber}")]
    public async Task<IActionResult> GetByTrackingNumber(string trackingNumber)
    {
        var shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(trackingNumber);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateShipmentRequest request)
    {
        var shipment = await _shipmentService.CreateShipmentAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = shipment.Id }, shipment);
    }

    // Replaces header and legs; only while Planned.
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateShipmentRequest request)
    {
        var shipment = await _shipmentService.UpdateShipmentAsync(id, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    // Carrier / vehicle / schedule / document changes on a leg that hasn't departed.
    [HttpPut("{id:int}/legs/{legId:int}")]
    public async Task<IActionResult> UpdateLeg(int id, int legId, UpdateShipmentLegRequest request)
    {
        var shipment = await _shipmentService.UpdateLegAsync(id, legId, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpGet("{id:int}/items")]
    public async Task<IActionResult> GetItems(int id)
    {
        var items = await _shipmentService.GetItemsAsync(id);
        if (items == null) return NotFound();

        return Ok(items);
    }

    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddItems(int id, ShipmentItemsRequest request)
    {
        var shipment = await _shipmentService.AddItemsAsync(id, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpDelete("{id:int}/items/{trackedItemId:int}")]
    public async Task<IActionResult> RemoveItem(int id, int trackedItemId)
    {
        var shipment = await _shipmentService.RemoveItemAsync(id, trackedItemId);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpPost("{id:int}/legs/{legId:int}/depart")]
    public async Task<IActionResult> DepartLeg(int id, int legId, LegMovementRequest request)
    {
        var shipment = await _shipmentService.DepartLegAsync(id, legId, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpPost("{id:int}/legs/{legId:int}/arrive")]
    public async Task<IActionResult> ArriveLeg(int id, int legId, LegMovementRequest request)
    {
        var shipment = await _shipmentService.ArriveLegAsync(id, legId, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpPost("{id:int}/customs")]
    public async Task<IActionResult> UpdateCustoms(int id, CustomsUpdateRequest request)
    {
        var shipment = await _shipmentService.UpdateCustomsAsync(id, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpPost("{id:int}/events")]
    public async Task<IActionResult> RecordEvent(int id, EventDetails request)
    {
        var result = await _shipmentService.RecordEventAsync(id, request);
        if (result == null) return NotFound();

        return Ok(result);
    }

    [HttpPost("{id:int}/deliver")]
    public async Task<IActionResult> Deliver(int id, DeliverShipmentRequest request)
    {
        var shipment = await _shipmentService.DeliverAsync(id, request);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var shipment = await _shipmentService.CancelAsync(id);
        if (shipment == null) return NotFound();

        return Ok(shipment);
    }
}
