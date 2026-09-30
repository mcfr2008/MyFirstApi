using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MyFirstApi.Interfaces;

namespace MyFirstApi.Controllers;

// Downloads stored files (signatures, delivery photos). Requires a logged-in user;
// ids are random UUIDs. Per-role access will be added with the permissions phase
// (signatures and photos are personal data under PDPA).
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IStoredFileService _storedFileService;

    public FilesController(IStoredFileService storedFileService)
    {
        _storedFileService = storedFileService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var result = await _storedFileService.OpenAsync(id);
        if (result == null) return NotFound();

        var (file, content) = result.Value;
        // Content never changes after upload, so its SHA-256 is a perfect ETag.
        Response.Headers[HeaderNames.CacheControl] = "private, max-age=86400, immutable";
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        return File(content, file.ContentType, enableRangeProcessing: false,
            entityTag: new EntityTagHeaderValue($"\"{file.Sha256}\""), lastModified: file.UploadedAt);
    }
}
