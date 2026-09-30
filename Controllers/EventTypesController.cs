using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class EventTypesController : MasterDataController<EventTypeRequest, EventTypeResponse, MasterDataQuery>
{
    public EventTypesController(IEventTypeService service) : base(service)
    {
    }
}
