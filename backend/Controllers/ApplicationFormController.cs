using Admissions.Dtos;
using Admissions.Models;
using Admissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize(Roles = AppRoles.Parent)]
public sealed class ApplicationFormController(
    IApplicationFormService forms,
    IWebHostEnvironment env) : ControllerBase
{
    string Nid => User.FindFirst("nid")?.Value ?? throw new UnauthorizedAccessException();
    string UploadDir
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("UPLOAD_DIR");
            var dir = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "uploads")
                : configured;
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [HttpGet("{holdId}/details")]
    public async Task<IActionResult> Get(string holdId) => Ok(await forms.GetDetailsAsync(holdId, Nid));

    [HttpPut("{holdId}/details")]
    public async Task<IActionResult> Submit(string holdId, [FromBody] SubmitDetailsRequest req)
        => Ok(await forms.SubmitDetailsAsync(holdId, Nid, req));

    [HttpPost("{holdId}/documents")]
    public async Task<IActionResult> Upload(string holdId, IFormFile file, [FromForm] string documentType)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "NO_FILE", message = "Choose a file." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { error = "TOO_LARGE", message = "Max 5 MB per file." });
        await using var stream = file.OpenReadStream();
        var doc = await forms.UploadDocumentAsync(holdId, Nid, documentType,
            file.FileName, file.ContentType, stream);
        return StatusCode(201, doc);
    }

    [HttpGet("{holdId}/documents")]
    public async Task<IActionResult> List(string holdId)
    {
        var d = await forms.GetDetailsAsync(holdId, Nid);
        return Ok(d.Documents);
    }

    [HttpGet("documents/{docId}/download")]
    public async Task<IActionResult> Download(string docId)
    {
        var found = await forms.GetDocumentForDownloadAsync(docId, Nid);
        if (found is null) return NotFound();
        var (stored, fileName, contentType) = found.Value;
        var path = Path.Combine(UploadDir, stored);
        if (!System.IO.File.Exists(path)) return NotFound();
        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, contentType, fileName);
    }

    [HttpDelete("documents/{docId}")]
    public async Task<IActionResult> Delete(string docId)
    {
        await forms.DeleteDocumentAsync(docId, Nid);
        return NoContent();
    }
}

[ApiController]
[Route("api/admin/applications")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminApplicationFormController(IApplicationFormService forms) : ControllerBase
{
    [HttpGet("{holdId}/details")]
    public async Task<IActionResult> Get(string holdId) => Ok(await forms.GetDetailsForAdminAsync(holdId));
}
