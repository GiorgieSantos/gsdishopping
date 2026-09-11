namespace GSDIShoppingApi.Models;

/// <summary>
/// Registro append-only (livro-razão) de cada nota fiscal enviada por um
/// usuário. O saldo de pontos NUNCA é guardado como um contador separado —
/// ele é sempre a soma dos PointsEarned das transações com status
/// Approved. Isso evita toda uma classe de bugs de concorrência (duas
/// notas processadas ao mesmo tempo dessincronizando um contador) e dá um
/// extrato auditável de graça.
/// </summary>
public class PointTransaction
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Loja onde a compra foi feita — resolvida pelo backend a partir do
    /// CNPJ da nota, nunca aceita "cru" do que o app envia. Uma
    /// PointTransaction só existe se o CNPJ correspondeu a uma loja
    /// cadastrada (é a checagem de elegibilidade); por isso o campo é
    /// obrigatório.
    /// </summary>
    public required int StoreId { get; set; }
    public Store? Store { get; set; }

    /// <summary>
    /// Chave de acesso de 44 dígitos da NFC-e/CF-e, lida do QR code.
    /// Índice único no banco — é a principal barreira contra pontuar a
    /// mesma compra duas vezes.
    /// </summary>
    public required string AccessKey { get; set; }

    public decimal TotalValue { get; set; }
    public int PointsEarned { get; set; }
    public ReceiptStatus Status { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidatedAt { get; set; }
}
