using Admissions.Models;
using Admissions.Dtos;
using Admissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminController(IAdmissionService svc, IAuthService auth) : ControllerBase
{
    // ── academic years ──
    [HttpGet("years")]
    public async Task<IActionResult> Years() => Ok(await svc.GetYearsAsync());

    [HttpPost("years")]
    public async Task<IActionResult> CreateYear([FromBody] CreateYearRequest req)
        => StatusCode(201, await svc.CreateYearAsync(req));

    [HttpPut("years/{id}/activate")]
    public async Task<IActionResult> ActivateYear(string id)
    {
        await svc.ActivateYearAsync(id);
        return Ok(new { message = "Academic year activated." });
    }

    // ── classes ──
    [HttpGet("classes")]
    public async Task<IActionResult> Classes([FromQuery] bool? activeOnly, [FromQuery] string? yearId)
        => Ok(await svc.GetClassesAsync(activeOnly, yearId, null));

    [HttpPost("classes")]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassRequest req)
        => StatusCode(201, await svc.CreateClassAsync(req));

    [HttpPut("classes/{id}")]
    public async Task<IActionResult> UpdateClass(string id, [FromBody] UpdateClassRequest req)
    {
        var c = await svc.UpdateClassAsync(id, req);
        return c is null ? NotFound() : Ok(c);
    }

    // ── applications ──
    [HttpGet("applications")]
    public async Task<IActionResult> Applications([FromQuery] ApplicationFilter filter)
        => Ok(await svc.GetApplicationsAsync(filter));

    // ── dashboard ──
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard() => Ok(await svc.GetDashboardAsync());

    // ── students ──
    [HttpGet("students")]
    public async Task<IActionResult> Students([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
                                               [FromQuery] string? search = null)
        => Ok(await auth.GetStudentsAsync(page, pageSize, search));
}
