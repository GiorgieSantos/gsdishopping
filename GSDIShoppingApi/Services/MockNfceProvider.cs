namespace GSDIShoppingApi.Services;

/// <summary>
/// INfceProvider "dublê", para testar o fluxo completo (app → fila →
/// worker → pontos) sem depender da Infosimples nem de rede. Usado em
/// Development (ver Program.cs) — em produção, o provider real é o
/// <see cref="InfoSimplesNfceProvider"/>.
///
/// Aprova qualquer chave com o formato certo (44 dígitos numéricos) e
/// extrai o CNPJ do emitente direto da própria chave (posições 7 a 20,
/// padrão ENCAT — mesma lógica que o app Flutter já usa em
/// NfceQrData.cnpj). Não devolve ValorTotal nem DataEmissao (ficam null) —
/// o NfceValidationWorker trata "provider não informou o valor oficial"
/// como "não dá para comparar, não manda para revisão por divergência",
/// então isso não trava o teste do fluxo.
///
/// Nota: existe também um projeto separado, ValidationMock/ (da Fase 1),
/// que simulava o antigo contrato síncrono via HTTP
/// (IExternalReceiptValidationService). Esse projeto continua no repo mas
/// não é mais chamado pelo fluxo novo — este mock aqui roda no mesmo
/// processo da API, sem precisar subir um segundo `dotnet run`.
/// </summary>
public class MockNfceProvider : INfceProvider
{
    public Task<NfceResult> ConsultarAsync(string chaveAcesso, CancellationToken cancellationToken)
    {
        var validFormat = chaveAcesso.Length == 44 && chaveAcesso.All(char.IsDigit);
        if (!validFormat)
        {
            return Task.FromResult(new NfceResult(
                Found: false, Authorized: false, Cancelled: false,
                EmitenteCnpj: null, ValorTotal: null, DataEmissao: null, ConsumidorCpf: null,
                RawStatus: "mock", Reason: "Chave de acesso com formato inválido (mock)."));
        }

        var cnpjEmitente = chaveAcesso.Substring(6, 14);

        return Task.FromResult(new NfceResult(
            Found: true, Authorized: true, Cancelled: false,
            EmitenteCnpj: cnpjEmitente,
            ValorTotal: null,
            DataEmissao: DateTime.UtcNow,
            ConsumidorCpf: null,
            RawStatus: "mock-aprovado",
            Reason: null));
    }
}
