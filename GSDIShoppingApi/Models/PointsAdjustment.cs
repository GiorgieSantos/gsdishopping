namespace GSDIShoppingApi.Models;

/// <summary>
/// Registro append-only de um ajuste manual de pontos feito por um admin
/// pelo painel (bônus, correção, estorno etc.). Segue o mesmo princípio de
/// <see cref="PointTransaction"/>: o saldo do usuário nunca é um contador —
/// é sempre a soma de PointsEarned aprovados em PointTransactions MAIS a
/// soma de PointsDelta aqui (ver PointsService.GetBalanceAsync). Não
/// reaproveitamos PointTransaction para isto porque ela exige StoreId (a
/// compra sempre aconteceu numa loja) — um ajuste manual não tem loja, e
/// misturar os dois conceitos numa tabela só tornaria StoreId
/// artificialmente opcional para todo mundo.
/// </summary>
public class PointsAdjustment
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>Positivo = crédito de pontos, negativo = débito/correção.</summary>
    public required int PointsDelta { get; set; }

    /// <summary>Motivo do ajuste, digitado pelo admin — obrigatório, é o que torna o ajuste auditável no extrato do cliente.</summary>
    public required string Reason { get; set; }

    public int AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
