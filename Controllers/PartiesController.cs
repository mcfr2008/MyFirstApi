using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class PartiesController : MasterDataController<PartyRequest, PartyResponse, PartyQuery>
{
    public PartiesController(IPartyService service) : base(service)
    {
    }
}
