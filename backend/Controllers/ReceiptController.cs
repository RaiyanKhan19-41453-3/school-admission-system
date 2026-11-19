using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Admissions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Admissions.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ReceiptController(IReceiptService receipts, IConfiguration config) : ControllerBase
{
    [HttpGet("{holdId}/receipt")]
    public async Task<IActionResult> Download(string holdId, [FromQuery] string? token = null)
    {
        // Accept the JWT from the query string so a plain browser link can download the PDF.
        token ??= Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(token))
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Sign in to download your receipt." });

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
            var nid = principal.FindFirst("nid")?.Value;
            if (string.IsNullOrWhiteSpace(nid)) return Unauthorized();

            var (bytes, fileName) = await receipts.GenerateAsync(holdId, nid);
            return File(bytes, "application/pdf", fileName);
        }
        catch
        {
            return Unauthorized(new { error = "UNAUTHORIZED", message = "Invalid or expired token." });
        }
    }
}
