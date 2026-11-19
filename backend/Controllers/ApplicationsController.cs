using Admissions.Dtos;
using Admissions.Services;
using Admissions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

/// <summary>Authenticated parent-facing applications with payment &amp; richer details.</summary>
[ApiController]
[Route("api/applications")]
[Authorize(Roles = AppRoles.Parent)]
public sealed class ApplicationsController(IAdmissionService svc) : ControllerBase
{
    string Nid => User.FindFirst("nid")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet("my")]
    public async Task<IActionResult> My() => Ok(await svc.GetMyHoldsAsync(Nid));

    [HttpPost("classes/{classId}/apply")]
    public async Task<IActionResult> Apply(string classId)
        => StatusCode(201, await svc.ApplyAsync(classId, Nid));

    [HttpPost("{holdId}/pay")]
    public async Task<IActionResult> Pay(string holdId, [FromBody] PayRequest req)
    {
        var h = await svc.ConfirmAsync(holdId, Nid, req);
        return Ok(new { hold = h, message = "Admission confirmed." });
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> Cancel(string holdId)
    {
        await svc.CancelAsync(holdId, Nid);
        return NoContent();
    }
}
