using GSDIShoppingApi.Data;
using GSDIShoppingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Services;

/// <summary>
/// Implementação de V1 de <see cref="INfceValidationQueue"/>: "a fila" é a
/// própria tabela PointTransactions, e "consumir da fila" é fazer polling
/// nela por linhas Status=Pending. Registrado como singleton (ver
/// Program.cs) porque quem o injeta é o NfceValidationWorker, que também é
/// singleton — por isso depende de IDbContextFactory (seguro para
/// singleton) em vez do DbContext scoped normal.
///
/// Limitação conhecida, aceitável para V1 (uma instância só do worker):
/// LeaseNextBatchAsync não marca as linhas como "em processamento" antes
/// de devolvê-las — só lê o status atual. Rodando um único worker isso não
/// causa duplicidade de consulta paga (o worker processa a lista uma nota
/// de cada vez, sequencialmente). Se um dia o worker escalar para mais de
/// uma instância, este é o lugar a trocar por SQS/RabbitMQ (que já dão
/// exclusividade de consumo de mensagem de graça) — ou, mantendo banco,
/// adicionar uma coluna tipo ClaimedAt/ClaimedBy.
/// </summary>
public class DatabaseNfceValidationQueue(IDbContextFactory<GSDIShoppingDbContext> dbFactory) : INfceValidationQueue
{
    public Task EnqueueAsync(int pointTransactionId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<IReadOnlyList<int>> LeaseNextBatchAsync(int maxItems, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PointTransactions
            .Where(t => t.Status == ReceiptStatus.Pending)
            .OrderBy(t => t.CreatedAt)
            .Take(maxItems)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }
}
