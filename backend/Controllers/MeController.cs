using Admissions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

/// <summary>Legacy /me/holds contract.</summary>
[ApiController]
[Route("me/holds")]
public sealed class MeController(IAdmissionService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var v = Request.Headers["X-Student-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(v))
            return BadRequest(new { error = "MISSING_STUDENT_ID", message = "X-Student-Id header is required." });
        return Ok(await svc.GetMyHoldsAsync(v.Trim()));
    }
}
