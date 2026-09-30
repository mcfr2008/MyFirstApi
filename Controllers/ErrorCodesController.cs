using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstApi.Exceptions;

namespace MyFirstApi.Controllers;

// The error catalog with English and Thai message templates, so the frontend
// can translate { code, args } from error responses. Public: it holds no data,
// and the login screen needs it before a token exists.
[AllowAnonymous]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ErrorCodesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(Errors.All.Select(e => new { e.Code, e.Status, e.En, e.Th }));
    }
}
