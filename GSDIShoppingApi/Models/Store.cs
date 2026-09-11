namespace GSDIShoppingApi.Models;

/// <summary>
/// Loja parceira do shopping. O CNPJ é a chave usada para conferir, no
/// momento do scan de nota fiscal, se a compra foi feita numa loja
/// elegível a dar pontos (ver <see cref="PointTransaction"/> e
/// PointsService.ReceiveReceiptAsync).
/// </summary>
public class Store
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Só dígitos (14 caracteres), sem máscara — normalizado no cadastro.</summary>
    public required string Cnpj { get; set; }

    public string? Floor { get; set; }
    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
}
