using GSDIShoppingApi.Models;

namespace GSDIShoppingApi.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(ApplicationUser user);

    /// <summary>Mesmo formato de token, mas com claim de role "Admin" — é o que a policy "AdminOnly" (ver Program.cs) exige.</summary>
    (string Token, DateTime ExpiresAt) GenerateAdminToken(AdminUser admin);
}
