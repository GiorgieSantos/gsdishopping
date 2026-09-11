using GSDIShoppingApi.Dtos.Receipts;

namespace GSDIShoppingApi.Services;

public interface IPointsService
{
    /// <summary>
    /// Intake da nota (Fase 4): faz só as checagens que não dependem do
    /// fornecedor fiscal (duplicidade, loja elegível) e, se passarem,
    /// grava a transação como PENDENTE para o NfceValidationWorker
    /// processar. Não aprova nem calcula pontos aqui — ver
    /// NfceValidationWorker.
    /// </summary>
    Task<ReceiveReceiptResponse> ReceiveReceiptAsync(
        int userId,
        ValidateReceiptRequest request,
        CancellationToken cancellationToken);

    /// <summary>Status atual de uma transação do próprio usuário (para o app acompanhar uma nota PENDENTE). Null se não existir ou não for do usuário.</summary>
    Task<ReceiptStatusResponse?> GetReceiptStatusAsync(
        int userId,
        int transactionId,
        CancellationToken cancellationToken);

    /// <summary>Saldo atual: soma de PointsEarned aprovados em PointTransactions MAIS soma de PointsDelta em PointsAdjustments. Nunca um contador.</summary>
    Task<int> GetBalanceAsync(int userId, CancellationToken cancellationToken);
}
