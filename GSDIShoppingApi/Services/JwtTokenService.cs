using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GSDIShoppingApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace GSDIShoppingApi.Services;

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public (string Token, DateTime ExpiresAt) GenerateToken(ApplicationUser user) =>
        BuildToken(
        [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
        ]);

    public (string Token, DateTime ExpiresAt) GenerateAdminToken(AdminUser admin) =>
        BuildToken(
        [
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Name),
            // É este claim que a policy "AdminOnly" (Program.cs) exige.
            new Claim(ClaimTypes.Role, "Admin"),
        ]);

    private (string Token, DateTime ExpiresAt) BuildToken(Claim[] claims)
    {
        var key = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key não configurado.");
        var expiryMinutes = config.GetValue("Jwt:ExpiryMinutes", 60);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
