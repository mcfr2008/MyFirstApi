using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class EmissionFactorsController : MasterDataController<EmissionFactorRequest, EmissionFactorResponse, EmissionFactorQuery>
{
    public EmissionFactorsController(IEmissionFactorService service) : base(service)
    {
    }
}
