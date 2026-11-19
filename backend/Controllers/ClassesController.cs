using Admissions.Dtos;
using Admissions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

/// <summary>Legacy /classes contract — kept so existing clients and tests keep working.</summary>
[ApiController]
[Route("classes")]
public sealed class ClassesController(IAdmissionService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activeOnly)
        => Ok(await svc.GetClassesAsync(activeOnly ?? true, null, null));

    [HttpGet("/api/years")]
    public async Task<IActionResult> Years() => Ok(await svc.GetYearsAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest req)
        => StatusCode(201, await svc.CreateClassAsync(req));

    [HttpGet("{classId}")]
    public async Task<IActionResult> Get(string classId)
    {
        var c = await svc.GetClassAsync(classId);
        return c is null ? NotFound(new { error = "NOT_FOUND", message = "Class not found." }) : Ok(c);
    }

    [HttpPost("{classId}/holds")]
    public async Task<IActionResult> Hold(string classId)
    {
        var nid = StudentHeader();
        if (nid is null) return BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });
        return StatusCode(201, await svc.ApplyAsync(classId, nid));
    }

    string? StudentHeader()
    {
        var v = Request.Headers["X-Student-Id"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
