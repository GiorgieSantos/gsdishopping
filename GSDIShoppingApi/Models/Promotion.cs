namespace GSDIShoppingApi.Models;

/// <summary>
/// Anúncio de desconto de uma loja (ex.: "20% OFF em calçados até
/// domingo") — sem custo em pontos e sem resgate, diferente de
/// <see cref="Coupon"/>. <see cref="StoreId"/> é opcional: nula significa
/// uma promoção do shopping como um todo, não de uma loja específica
/// (ex.: "Estacionamento grátis nas quintas"). Quando a loja é apagada, a
/// promoção não é apagada junto — só deixa de estar ligada a uma loja (ver
/// DeleteBehavior.SetNull em GSDIShoppingDbContext).
/// </summary>
public class Promotion
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }

    /// <summary>Texto livre — "20% OFF", "Leve 2 pague 1", "R$10 de desconto" etc.</summary>
    public required string DiscountLabel { get; set; }

    public int? StoreId { get; set; }
    public Store? Store { get; set; }

    public string? ImageUrl { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
