namespace GSDIShoppingApi.Services;

/// <summary>
/// Leitura dos parâmetros configuráveis pelo painel (ver
/// Models/SystemParameter.cs e Components/Pages/Admin/Parametros.razor).
/// Sem cache de propósito: o volume de leituras (uma por nota processada
/// pelo worker) é baixo, e cache errado — parâmetro mudado no painel sem
/// o worker perceber — é pior do que uma consulta a mais no banco.
/// </summary>
public interface ISystemParametersService
{
    /// <summary>Devolve o Value da chave, ou <paramref name="defaultValue"/> se a chave não existir ainda no banco (ex.: banco criado antes do seed).</summary>
    Task<string> GetValueAsync(string key, string defaultValue, CancellationToken cancellationToken);
}
