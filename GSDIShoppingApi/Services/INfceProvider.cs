namespace GSDIShoppingApi.Services;

/// <summary>
/// O que o fornecedor fiscal (Infosimples, NFe.io, SEFAZ direto etc.)
/// devolveu sobre uma NFC-e específica. Quem decide o que fazer com isso —
/// aprovar, mandar para revisão, rejeitar — é o <c>NfceValidationWorker</c>,
/// nunca o provider: o provider só relata o que encontrou.
/// </summary>
/// <param name="Found">A chave existe na base do fornecedor? Se false, os demais campos vêm nulos.</param>
/// <param name="Authorized">A nota está autorizada pela SEFAZ (não é rascunho/denegada)?</param>
/// <param name="Cancelled">A nota foi cancelada depois de emitida?</param>
/// <param name="EmitenteCnpj">CNPJ do emitente, só dígitos, conforme consta oficialmente na nota.</param>
/// <param name="ValorTotal">Valor total oficial da nota — é contra isto que comparamos o valor informado pelo app (regra "valor divergente → revisão").</param>
/// <param name="DataEmissao">Data/hora oficial de emissão — usada na checagem de "nota fora do período da campanha".</param>
/// <param name="ConsumidorCpf">CPF do consumidor gravado na nota, só dígitos, quando a nota identificou o comprador. Null se a nota não tem CPF do consumidor (comum, é opcional na NFC-e).</param>
/// <param name="RawStatus">Texto/código de status cru devolvido pelo fornecedor, guardado só para log/depuração.</param>
/// <param name="Reason">Motivo legível para o caso de Found=false ou erro de consulta (nota não encontrada, timeout, etc.).</param>
public record NfceResult(
    bool Found,
    bool Authorized,
    bool Cancelled,
    string? EmitenteCnpj,
    decimal? ValorTotal,
    DateTime? DataEmissao,
    string? ConsumidorCpf,
    string? RawStatus,
    string? Reason)
{
    /// <summary>Nota existe, está autorizada e não foi cancelada — a checagem "a nota é real" passou.</summary>
    public bool IsValid => Found && Authorized && !Cancelled;
}

/// <summary>
/// Contrato com o fornecedor fiscal que confirma se uma NFC-e é real e
/// está autorizada. Isto é o que fica atrás do worker da fila — trocar de
/// fornecedor (Infosimples → NFe.io, ou somar um segundo como fallback) é
/// só trocar/somar uma implementação desta interface, sem tocar no resto
/// do sistema (NfceValidationWorker, PointsService, controllers).
///
/// Substitui a antiga IExternalReceiptValidationService da Fase 1: aquela
/// via só "aprovado sim/não" porque toda a validação acontecia de forma
/// síncrona, no meio do request HTTP. A partir da Fase 4 o worker é quem
/// aplica as regras de negócio (duplicidade, elegibilidade, valor
/// divergente, CPF divergente) em cima do que o provider devolve — por
/// isso o provider agora devolve os dados crus da nota, não um veredito.
/// </summary>
public interface INfceProvider
{
    Task<NfceResult> ConsultarAsync(string chaveAcesso, CancellationToken cancellationToken);
}
