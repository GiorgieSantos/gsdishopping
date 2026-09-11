namespace GSDIShoppingApi.Models;

/// <summary>
/// Campanha do shopping (ex.: "Semana da Moda") — agrupa um ou mais
/// <see cref="Coupon"/> que o cliente pode ver/resgatar enquanto a
/// campanha estiver ativa. Diferente de <see cref="Promotion"/>: campanha
/// é sempre do shopping como um todo (não de uma loja específica) e existe
/// para organizar cupons, não para anunciar um desconto pontual.
/// </summary>
public class Campaign
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Coupon> Coupons { get; set; } = new List<Coupon>();
}
