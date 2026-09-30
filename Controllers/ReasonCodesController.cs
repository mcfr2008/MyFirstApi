using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Dtos;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public class ReasonCodesController : MasterDataController<ReasonCodeRequest, ReasonCodeResponse, ReasonCodeQuery>
{
    public ReasonCodesController(IReasonCodeService service) : base(service)
    {
    }
}
