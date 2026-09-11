namespace GSDIShoppingApi.Models;

/// <summary>
/// Usuário do painel de administração — cadastro separado do
/// <see cref="ApplicationUser"/> (cliente final). Quem administra o
/// programa de fidelidade (cadastra lojas/campanhas/cupons/promoções e
/// ajusta pontos manualmente) usa este login, protegido pela policy
/// "AdminOnly" (ver Program.cs). O primeiro AdminUser só pode ser criado
/// enquanto não existir nenhum ainda (ver AdminAuthController.Register) —
/// depois disso, o endpoint de registro fica bloqueado de propósito, ao
/// contrário do padrão temporário e aberto do CatalogController.
/// </summary>
public class AdminUser
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PointsAdjustment> PointsAdjustments { get; set; } = new List<PointsAdjustment>();
}
