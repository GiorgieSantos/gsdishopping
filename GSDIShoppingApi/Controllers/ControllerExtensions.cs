using System.Security.Claims;

namespace GSDIShoppingApi.Controllers;

internal static class ControllerExtensions
{
    /// <summary>Extrai o id do usuário autenticado a partir do claim "sub" do JWT.</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (sub is null || !int.TryParse(sub, out var id))
        {
            throw new InvalidOperationException("Token sem claim de usuário válido.");
        }
        return id;
    }

    /// <summary>Mesma extração de <see cref="GetUserId"/>, com nome mais claro quando o token é de um AdminUser em vez de um ApplicationUser.</summary>
    public static int GetAdminId(this ClaimsPrincipal user) => user.GetUserId();
}
