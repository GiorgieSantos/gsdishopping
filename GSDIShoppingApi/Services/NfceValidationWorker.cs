using GSDIShoppingApi.Data;
using GSDIShoppingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Services;

/// <summary>
/// O "worker" da Fase 4: roda dentro do próprio processo da API (hospedado
/// via AddHostedService, ver Program.cs) e fica em loop, em background,
/// puxando notas PENDENTES da fila (<see cref="INfceValidationQueue"/>),
/// consultando o fornecedor fiscal (<see cref="INfceProvider"/>) e
/// aplicando as regras de negócio da tabela que o Carlos desenhou:
///
///   Nota cancelada?             → REJEITAR
///   CNPJ não bate com a loja?   → REJEITAR (nova checagem — o app pode
///                                  ter mandado um CNPJ que não é o da
///                                  chave; a checagem de elegibilidade no
///                                  intake já resolveu a loja pelo CNPJ
///                                  informado, mas só o fornecedor confirma
///                                  o CNPJ OFICIAL da nota)
///   Valor divergente?           → REVISÃO
///   CPF diferente do usuário?   → conforme parâmetro CpfDivergenteAction
///   Nota válida/autorizada?     → APROVAR
///
/// "Nota fora do período da campanha" (do desenho original) NÃO está
/// implementada ainda — o schema atual não liga PointTransaction a uma
/// Campaign nem tem o conceito de "período em que pontuar vale" fora do
/// catálogo de cupons resgatáveis. Fica como próximo passo quando esse
/// conceito existir de verdade no modelo de dados.
///
/// Limitação conhecida, documentada também no README: não há política de
/// retry/backoff nem fila de erro (DLQ) ainda — se ConsultarAsync lançar
/// uma exceção (timeout, fornecedor fora do ar), a linha continua
/// PENDENTE e é tentada de novo na próxima rodada de polling,
/// indefinidamente. Para produção, isso deveria ganhar um limite de
/// tentativas com um estado terminal de erro (não existe hoje).
/// </summary>
public class NfceValidationWorker(
    IDbContextFactory<GSDIShoppingDbContext> dbFactory,
    INfceValidationQueue queue,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<NfceValidationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(config.GetValue("NfceQueue:PollIntervalSeconds", 5));
        var batchSize = config.GetValue("NfceQueue:BatchSize", 10);

        logger.LogInformation(
            "NfceValidationWorker iniciado (poll a cada {PollInterval}, lote de {BatchSize}).",
            pollInterval, batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ids = await queue.LeaseNextBatchAsync(batchSize, stoppingToken);
                foreach (var id in ids)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessOneAsync(id, stoppingToken);
                }

                if (ids.Count == 0)
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Desligamento normal do host — não é erro.
            }
            catch (Exception ex)
            {
                // Erro no laço em si (ex.: banco fora do ar) — não deixa o
                // BackgroundService morrer, só loga e tenta de novo depois
                // do intervalo normal.
                logger.LogError(ex, "Erro no laço principal do NfceValidationWorker.");
                await Task.Delay(pollInterval, stoppingToken);
            }
        }
    }

    private async Task ProcessOneAsync(int transactionId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var transaction = await db.PointTransactions
            .Include(t => t.Store)
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.Id == transactionId, ct);

        // Já foi processada (ex.: corrida entre duas rodadas, ou um admin
        // mexeu manualmente) — não há nada a fazer.
        if (transaction is null || transaction.Status != ReceiptStatus.Pending)
        {
            return;
        }

        // INfceProvider e ISystemParametersService resolvidos por escopo
        // próprio (não injetados direto no worker, que é singleton) —
        // assim cada consulta usa uma instância "fresca", sem prender
        // recursos scoped (como o HttpClient tipado) pela vida inteira do
        // processo.
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<INfceProvider>();
        var parameters = scope.ServiceProvider.GetRequiredService<ISystemParametersService>();

        NfceResult result;
        try
        {
            result = await provider.ConsultarAsync(transaction.AccessKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Falha ao consultar o fornecedor fiscal para a transação {TransactionId} — mantida PENDENTE para nova tentativa.",
                transactionId);
            return;
        }

        var (status, reason) = await DecideAsync(transaction, result, parameters, ct);

        transaction.Status = status;
        transaction.RejectionReason = reason;
        transaction.ValidatedAt = DateTime.UtcNow;
        transaction.PointsEarned = status == ReceiptStatus.Approved
            ? (int)Math.Floor(transaction.TotalValue / config.GetValue("Points:ReaisPerPoint", 1.0m))
            : 0;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Transação {TransactionId} (chave {AccessKey}) processada: {Status}.",
            transactionId, transaction.AccessKey, status);
    }

    private static async Task<(ReceiptStatus Status, string? Reason)> DecideAsync(
        PointTransaction transaction, NfceResult result, ISystemParametersService parameters, CancellationToken ct)
    {
        if (!result.Found || !result.Authorized)
        {
            return (ReceiptStatus.RejectedInvalid, result.Reason ?? "Nota não encontrada/autorizada junto ao fornecedor fiscal.");
        }

        if (result.Cancelled)
        {
            return (ReceiptStatus.RejectedInvalid, "Esta nota fiscal foi cancelada.");
        }

        if (result.EmitenteCnpj is not null && transaction.Store is not null
            && result.EmitenteCnpj != transaction.Store.Cnpj)
        {
            return (ReceiptStatus.RejectedInvalid, "O CNPJ oficial da nota não corresponde à loja informada.");
        }

        if (result.ValorTotal is decimal valorOficial
            && Math.Abs(valorOficial - transaction.TotalValue) > 0.01m)
        {
            return (ReceiptStatus.PendingReview,
                $"Valor divergente: informado R$ {transaction.TotalValue:F2}, oficial R$ {valorOficial:F2}.");
        }

        if (result.ConsumidorCpf is not null && transaction.User is not null
            && result.ConsumidorCpf != transaction.User.Cpf)
        {
            var action = await parameters.GetValueAsync(
                "CpfDivergenteAction", CpfDivergenteAction.Revisar, ct);

            return action switch
            {
                CpfDivergenteAction.Aprovar => (ReceiptStatus.Approved, null),
                CpfDivergenteAction.Rejeitar => (ReceiptStatus.RejectedInvalid, "CPF da nota diferente do usuário logado."),
                _ => (ReceiptStatus.PendingReview, "CPF da nota diferente do usuário logado — aguardando revisão."),
            };
        }

        return (ReceiptStatus.Approved, null);
    }
}
