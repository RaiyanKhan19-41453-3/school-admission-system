using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

using Admissions.Models;

namespace Admissions.Controllers;

/// <summary>Validates a JWT from the query string for download links.</summary>
public static class DownloadToken
{
    public static string? Validate(string? token, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
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
            var p = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            var role = p.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return role == AppRoles.Admin ? (p.FindFirst("nid")?.Value ?? "admin") : null;
        }
        catch { return null; }
    }
}
