namespace GSDIShoppingApi.Models;

/// <summary>
/// Algo que o cliente resgata/usa gastando pontos — sempre pertence a uma
/// <see cref="Campaign"/>. Isto é o que distingue Coupon de Promotion (ver
/// <see cref="Promotion"/>): cupom tem custo em pontos e um fluxo de
/// resgate (o resgate em si ainda não foi implementado — Fase 2 cobre só a
/// leitura do catálogo); promoção é só um anúncio de desconto de uma loja,
/// sem resgate.
/// </summary>
public class Coupon
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required int PointsCost { get; set; }

    public required int CampaignId { get; set; }
    public Campaign? Campaign { get; set; }

    public string? ImageUrl { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
