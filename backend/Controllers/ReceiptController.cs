using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Admissions.Models;
using Admissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Admissions.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize(Roles = AppRoles.Parent)]
public sealed class ReceiptController(IReceiptService receipts, IConfiguration config) : ControllerBase
{
    string Nid => User.FindFirst("nid")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet("{holdId}/receipt")]
    [AllowAnonymous] // browser download links carry ?token= instead of an Authorization header
    public async Task<IActionResult> Download(string holdId, [FromQuery] string? token = null)
    {
        string? nid;
        if (!string.IsNullOrWhiteSpace(token))
        {
            // Query token (plain browser download links) — validate manually.
            try
            {
                var jwt = config.GetSection("Jwt");
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
                };
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
                nid = principal.FindFirst("nid")?.Value;
            }
            catch { return Unauthorized(new { error = "UNAUTHORIZED", message = "Invalid or expired token." }); }
        }
        else
        {
            // Standard Bearer header — already validated by the auth middleware.
            nid = User.FindFirst("nid")?.Value;
        }
        if (string.IsNullOrWhiteSpace(nid))
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Sign in to download your receipt." });

        var (bytes, fileName) = await receipts.GenerateAsync(holdId, nid);
        return File(bytes, "application/pdf", fileName);
    }
}
