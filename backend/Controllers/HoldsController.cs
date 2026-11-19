using Admissions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

/// <summary>Legacy /holds contract.</summary>
[ApiController]
[Route("holds")]
public sealed class HoldsController(IAdmissionService svc) : ControllerBase
{
    [HttpGet("{holdId}")]
    public async Task<IActionResult> Get(string holdId)
    {
        var h = await svc.GetHoldAsync(holdId);
        return h is null ? NotFound(new { error = "NOT_FOUND", message = "Hold not found." }) : Ok(h);
    }

    [HttpPost("{holdId}/confirm")]
    public async Task<IActionResult> Confirm(string holdId)
    {
        var nid = StudentHeader();
        if (nid is null) return BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });
        return Ok(await svc.ConfirmAsync(holdId, nid));
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> Cancel(string holdId)
    {
        var nid = StudentHeader();
        if (nid is null) return BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });
        await svc.CancelAsync(holdId, nid);
        return NoContent();
    }

    string? StudentHeader()
    {
        var v = Request.Headers["X-Student-Id"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
