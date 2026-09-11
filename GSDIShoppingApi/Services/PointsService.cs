using GSDIShoppingApi.Data;
using GSDIShoppingApi.Dtos.Receipts;
using GSDIShoppingApi.Models;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace GSDIShoppingApi.Services;

/// <summary>
/// Intake de notas fiscais (Fase 4): faz só a parte "nossa responsabilidade"
/// da checagem — duplicidade e loja elegível — e grava a transação como
/// PENDENTE para o NfceValidationWorker consultar o fornecedor fiscal de
/// forma assíncrona. Antes da Fase 4, este serviço fazia tudo de forma
/// síncrona (inclusive a chamada ao serviço externo, dentro do próprio
/// request HTTP) — ver ARQUITETURA_TECNICA.md §11 para o porquê da mudança
/// (principalmente: não prender o request HTTP na latência de um fornecedor
/// pago, e poder aplicar a deduplicação ANTES de gastar uma consulta paga).
/// </summary>
public class PointsService(
    GSDIShoppingDbContext db,
    INfceValidationQueue queue) : IPointsService
{
    public async Task<ReceiveReceiptResponse> ReceiveReceiptAsync(
        int userId,
        ValidateReceiptRequest request,
        CancellationToken cancellationToken)
    {
        // 1) Barreira rápida e barata: já existe uma nota com esta chave?
        //    Evita até criar a linha PENDENTE à toa (e, mais adiante,
        //    evita gastar uma consulta paga ao fornecedor fiscal).
        var alreadyExists = await db.PointTransactions
            .AnyAsync(t => t.AccessKey == request.AccessKey, cancellationToken);

        if (alreadyExists)
        {
            return new ReceiveReceiptResponse(0, nameof(ReceiptStatus.RejectedDuplicate), "Esta nota fiscal já foi utilizada.");
        }

        // 2) Checagem de elegibilidade: o CNPJ da nota precisa ser de uma
        //    loja cadastrada no shopping. Resolvemos a loja aqui, no
        //    servidor, a partir do CNPJ — nunca confiamos num StoreId que
        //    viesse do app. Isto não precisa do fornecedor fiscal, então
        //    continua acontecendo de forma síncrona, antes de enfileirar
        //    (é "nossa responsabilidade", não do provedor — ver a divisão
        //    de responsabilidades combinada com o Carlos).
        var cnpjDigits = DocumentValidators.OnlyDigits(request.StoreCnpj);
        var store = await db.Stores.SingleOrDefaultAsync(s => s.Cnpj == cnpjDigits, cancellationToken);
        if (store is null)
        {
            return new ReceiveReceiptResponse(0, nameof(ReceiptStatus.RejectedInvalid), "Esta nota é de uma loja que não faz parte do shopping.");
        }

        // 3) Grava PENDENTE e enfileira. O índice único de AccessKey (ver
        //    GSDIShoppingDbContext) é a garantia final contra duas notas
        //    concorrentes com a mesma chave — a checagem do passo 1 é só
        //    a barreira rápida, não a garantia.
        var transaction = new PointTransaction
        {
            UserId = userId,
            StoreId = store.Id,
            AccessKey = request.AccessKey,
            TotalValue = request.TotalValue,
            PointsEarned = 0,
            Status = ReceiptStatus.Pending,
        };

        db.PointTransactions.Add(transaction);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
        {
            // Corrida rara: duas requisições com a mesma chave passaram
            // pela checagem do passo 1 ao mesmo tempo. Garante, mesmo sob
            // concorrência, que no máximo uma delas chega a enfileirar —
            // e portanto no máximo uma consulta paga é feita por chave.
            return new ReceiveReceiptResponse(0, nameof(ReceiptStatus.RejectedDuplicate), "Esta nota fiscal já foi utilizada.");
        }

        await queue.EnqueueAsync(transaction.Id, cancellationToken);

        return new ReceiveReceiptResponse(
            transaction.Id,
            nameof(ReceiptStatus.Pending),
            "Nota recebida! Estamos validando junto ao fisco — acompanhe o resultado no seu extrato.");
    }

    public async Task<ReceiptStatusResponse?> GetReceiptStatusAsync(
        int userId, int transactionId, CancellationToken cancellationToken)
    {
        var transaction = await db.PointTransactions
            .SingleOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var balance = await GetBalanceAsync(userId, cancellationToken);
        return new ReceiptStatusResponse(
            transaction.Id,
            transaction.Status.ToString(),
            transaction.PointsEarned,
            transaction.RejectionReason,
            balance);
    }

    /// <summary>MySQL error 1062 = "Duplicate entry" (violação de índice único).</summary>
    private static bool IsDuplicateKeyViolation(DbUpdateException ex) =>
        ex.InnerException is MySqlException { Number: 1062 };

    public async Task<int> GetBalanceAsync(int userId, CancellationToken cancellationToken)
    {
        var fromTransactions = await db.PointTransactions
            .Where(t => t.UserId == userId && t.Status == ReceiptStatus.Approved)
            .SumAsync(t => (int?)t.PointsEarned, cancellationToken) ?? 0;

        // Ajustes manuais de admin (bônus/correção/estorno) entram no
        // mesmo somatório — ver Models/PointsAdjustment.cs.
        var fromAdjustments = await db.PointsAdjustments
            .Where(a => a.UserId == userId)
            .SumAsync(a => (int?)a.PointsDelta, cancellationToken) ?? 0;

        return fromTransactions + fromAdjustments;
    }
}
