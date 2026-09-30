using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class ContainersController : MasterDataController<ContainerRequest, ContainerResponse, ContainerQuery>
{
    private readonly IContainerService _containerService;

    public ContainersController(IContainerService service) : base(service)
    {
        _containerService = service;
    }

    // Everything inside, as a tree of nested containers and items.
    [HttpGet("{id:int}/contents")]
    public async Task<IActionResult> GetContents(int id)
    {
        var contents = await _containerService.GetContentsAsync(id);
        if (contents == null) return NotFound();

        return Ok(contents);
    }

    [HttpPost("{id:int}/load")]
    public async Task<IActionResult> Load(int id, ContainerLoadRequest request)
    {
        var result = await _containerService.LoadAsync(id, request);
        if (result == null) return NotFound();

        return Ok(result);
    }

    [HttpPost("{id:int}/unload")]
    public async Task<IActionResult> Unload(int id, ContainerUnloadRequest request)
    {
        var result = await _containerService.UnloadAsync(id, request);
        if (result == null) return NotFound();

        return Ok(result);
    }

    // One scan of the container records the event for every item inside it.
    [HttpPost("{id:int}/scan")]
    public async Task<IActionResult> Scan(int id, EventDetails request)
    {
        var result = await _containerService.ScanAsync(id, request);
        if (result == null) return NotFound();

        return Ok(result);
    }
}
