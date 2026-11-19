using System.Security.Cryptography;
using Admissions.Dtos;
using Admissions.Models;
using Admissions.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Admissions.Services;

public interface IAuthService
{
    Task<LoginResponse> RegisterAsync(RegisterRequest req);
    Task<LoginResponse> LoginAsync(LoginRequest req);
    Task SeedAdminAsync(string nid, string fullName, string password);
    Task<StudentResponse> GetProfileAsync(string nid);
    Task<StudentResponse> UpdateProfileAsync(string nid, UpdateProfileRequest req);
    Task<PagedResponse<StudentResponse>> GetStudentsAsync(int page, int pageSize, string? search);
}

public sealed class AuthService(
    IStudentRepository students,
    IConfiguration config) : IAuthService
{
    static string NewId(string p) => p + Guid.NewGuid().ToString("N")[0..12].ToLowerInvariant();

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 3 || parts[0] != "pbkdf2") return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    string GenerateToken(Student s)
    {
        var cfg = config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(int.TryParse(cfg["ExpiryMinutes"], out var m) ? m : 480);
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, s.Id),
            new System.Security.Claims.Claim("nid", s.Nid),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, s.Role),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, s.FullName)
        };
        var token = new JwtSecurityToken(cfg["Issuer"], cfg["Audience"], claims,
            expires: expires, signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest req)
    {
        if (await students.GetByNidAsync(req.Nid) is not null)
            throw new ConflictException("NID_EXISTS", "An account with this NID already exists.");
        var s = new Student
        {
            Id = NewId("s_"), Nid = req.Nid, FullName = req.FullName.Trim(),
            Phone = req.Phone, Email = req.Email,
            PasswordHash = HashPassword(req.Password), Role = AppRoles.Parent
        };
        await students.AddAsync(s);
        var token = GenerateToken(s);
        return new LoginResponse(token, DateTime.UtcNow.AddMinutes(480), s.Role, s.FullName);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        var s = await students.GetByNidAsync(req.Nid) ?? throw new UnauthorizedException("Invalid NID or password.");
        if (string.IsNullOrEmpty(s.PasswordHash) || !VerifyPassword(req.Password, s.PasswordHash))
            throw new UnauthorizedException("Invalid NID or password.");
        return new LoginResponse(GenerateToken(s), DateTime.UtcNow.AddMinutes(480), s.Role, s.FullName);
    }

    public async Task SeedAdminAsync(string nid, string fullName, string password)
    {
        if (await students.GetByNidAsync(nid) is not null) return;
        await students.AddAsync(new Student
        {
            Id = NewId("s_"), Nid = nid, FullName = fullName,
            PasswordHash = HashPassword(password), Role = AppRoles.Admin
        });
    }

    public async Task<StudentResponse> GetProfileAsync(string nid)
    {
        var s = await students.GetByNidAsync(nid) ?? throw new NotFoundException("Student not found.");
        return ToResponse(s);
    }

    public async Task<StudentResponse> UpdateProfileAsync(string nid, UpdateProfileRequest req)
    {
        var s = await dbStudent(nid);
        s.FullName = req.FullName.Trim(); s.Phone = req.Phone; s.Email = req.Email;
        await students.UpdateAsync(s);
        return ToResponse(s);
    }

    async Task<Student> dbStudent(string nid)
        => await students.GetByNidAsync(nid) ?? throw new NotFoundException("Student not found.");

    public async Task<PagedResponse<StudentResponse>> GetStudentsAsync(int page, int pageSize, string? search)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var list = await students.GetAllAsync(page, pageSize, search);
        var total = await students.CountAsync();
        return new PagedResponse<StudentResponse>(list.Select(ToResponse).ToList(), page, pageSize, total);
    }

    static StudentResponse ToResponse(Student s) => new(
        s.Id, s.Nid, s.FullName, s.Phone, s.Email, s.Role, s.CreatedAt, s.Holds?.Count ?? 0);
}
