using Admissions.Dtos;
using Admissions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        => Ok(await auth.RegisterAsync(req));

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
        => Ok(await auth.LoginAsync(req));

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var nid = User.FindFirst("nid")?.Value;
        if (nid is null) return Unauthorized();
        return Ok(await auth.GetProfileAsync(nid));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        var nid = User.FindFirst("nid")?.Value;
        if (nid is null) return Unauthorized();
        return Ok(await auth.UpdateProfileAsync(nid, req));
    }
}
