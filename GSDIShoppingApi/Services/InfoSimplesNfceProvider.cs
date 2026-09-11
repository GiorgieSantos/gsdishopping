using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GSDIShoppingApi.Services;

/// <summary>
/// INfceProvider real, usando a Infosimples ("SEFAZ / RJ / NFC-e
/// Resumida" — https://infosimples.com/consultas/sefaz-rj-nfce-resumida/)
/// como fonte. Pesquisa: como a origem dos dados desse produto é a própria
/// página pública de consulta da Fazenda do RJ (consultadfe.fazenda.rj.gov.br),
/// não deveria exigir certificado digital nem login no portal — só o
/// token da sua conta Infosimples e a chave de acesso.
///
/// IMPORTANTE — verifique antes de ir para produção:
/// Não tivemos acesso à documentação com autenticação da sua conta
/// Infosimples (fica atrás de login em api.infosimples.com/consultas/docs),
/// então a rota e os nomes de campo abaixo seguem a convenção pública que a
/// Infosimples documenta para as consultas de SEFAZ/NFC-e (envelope
/// { code, code_message, data: [...] }, campos como chave_acesso, cnpj,
/// valor_total, data_emissao, cancelada, cpf) — mas o único jeito de ter
/// 100% de certeza é rodar uma consulta real com o seu token e conferir a
/// resposta. Se algum nome de campo ou a rota estiverem diferentes, este é
/// o único arquivo que precisa mudar (é para isso que existe o
/// INfceProvider — o resto do sistema não sabe nem se importa com o
/// formato da Infosimples).
/// </summary>
public class InfoSimplesNfceProvider(HttpClient httpClient, IConfiguration config) : INfceProvider
{
    public async Task<NfceResult> ConsultarAsync(string chaveAcesso, CancellationToken cancellationToken)
    {
        var token = config["InfoSimples:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            // Falha "alto e claro" em vez de mandar a chamada sem token e
            // deixar a Infosimples devolver um 401 confuso — configuração
            // ausente é erro de deploy, não caso de negócio.
            throw new InvalidOperationException(
                "InfoSimples:Token não configurado (appsettings ou variável de ambiente).");
        }

        var route = config["InfoSimples:NfceRoute"]
            ?? "api/v2/consultas/sefaz/rj/nfce-resumida";

        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = token,
                ["nfce"] = chaveAcesso,
                // timeout em segundos que a própria Infosimples deve
                // esperar a SEFAZ responder antes de desistir — evita que
                // uma consulta lenta prenda o worker indefinidamente.
                ["timeout"] = config["InfoSimples:TimeoutSeconds"] ?? "300",
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new NfceResult(
                Found: false, Authorized: false, Cancelled: false,
                EmitenteCnpj: null, ValorTotal: null, DataEmissao: null, ConsumidorCpf: null,
                RawStatus: $"HTTP {(int)response.StatusCode}",
                Reason: $"Infosimples retornou {(int)response.StatusCode} — tratado como falha temporária.");
        }

        var payload = await response.Content.ReadFromJsonAsync<InfoSimplesEnvelope>(
            JsonOpts, cancellationToken);

        // code 200 = sucesso; a Infosimples usa outros códigos (ex.: 612)
        // para "não encontrado"/"erro de consulta" — nesses casos data
        // vem vazio e o motivo está em code_message ou errors.
        var item = payload?.Data?.FirstOrDefault();
        if (payload is null || payload.Code != 200 || item is null)
        {
            var reason = payload?.Errors?.FirstOrDefault() ?? payload?.CodeMessage ?? "Nota não encontrada.";
            return new NfceResult(
                Found: false, Authorized: false, Cancelled: false,
                EmitenteCnpj: null, ValorTotal: null, DataEmissao: null, ConsumidorCpf: null,
                RawStatus: payload?.CodeMessage,
                Reason: reason);
        }

        return new NfceResult(
            Found: true,
            // A Infosimples só devolve dado de nota autorizada nesse
            // produto (não expõe "denegada"/"rascunho") — se achou e não
            // está marcada como cancelada, consideramos autorizada.
            Authorized: true,
            Cancelled: item.Cancelada ?? false,
            EmitenteCnpj: OnlyDigits(item.Cnpj),
            ValorTotal: ParseDecimal(item.ValorTotal),
            DataEmissao: ParseDate(item.DataEmissao),
            ConsumidorCpf: string.IsNullOrWhiteSpace(item.Cpf) ? null : OnlyDigits(item.Cpf),
            RawStatus: payload.CodeMessage,
            Reason: null);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string? OnlyDigits(string? value) =>
        value is null ? null : new string(value.Where(char.IsDigit).ToArray());

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private sealed record InfoSimplesEnvelope(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("code_message")] string? CodeMessage,
        [property: JsonPropertyName("data")] List<InfoSimplesData>? Data,
        [property: JsonPropertyName("errors")] List<string>? Errors);

    private sealed record InfoSimplesData(
        [property: JsonPropertyName("chave_acesso")] string? ChaveAcesso,
        [property: JsonPropertyName("cnpj")] string? Cnpj,
        [property: JsonPropertyName("cpf")] string? Cpf,
        [property: JsonPropertyName("valor_total")] string? ValorTotal,
        [property: JsonPropertyName("data_emissao")] string? DataEmissao,
        [property: JsonPropertyName("cancelada")] bool? Cancelada);
}
